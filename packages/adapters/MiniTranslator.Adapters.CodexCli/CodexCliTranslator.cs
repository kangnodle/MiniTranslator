using System.ComponentModel;
using System.Text;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.CodexCli;

internal sealed class CodexCliTranslator(
    Func<ICodexProcess> startProcess,
    TimeSpan timeout,
    Func<EngineOptions>? currentOptions = null) : ITranslator, IDisposable
{
    public const int MaxTurnsPerSession = 20;

    private readonly SemaphoreSlim _turn = new(1, 1);
    private CodexConnection? _session;
    private string? _threadId;
    private int _turns;
    private EngineOptions _sessionOptions = EngineOptions.Default;

    public async Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _turn.WaitAsync(cancellationToken);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);

            var options = currentOptions?.Invoke() ?? EngineOptions.Default;
            if (options != _sessionOptions)
            {
                EndSession();
                _sessionOptions = options;
            }

            var reusedSession = _session is not null && _turns < MaxTurnsPerSession;
            var result = await ExchangeAsync(request, deadline.Token);

            if (result is null && reusedSession)
            {
                result = await ExchangeAsync(request, deadline.Token);
            }

            return result ?? TranslationErrors.CodexFailed(null);
        }
        catch (Win32Exception)
        {
            return TranslationErrors.CodexUnavailable;
        }
        catch (OperationCanceledException)
        {
            EndSession();
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return TranslationErrors.CodexTimedOut;
        }
        finally
        {
            _turn.Release();
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _turn.WaitAsync(cancellationToken);
        try
        {
            EndSession();
        }
        finally
        {
            _turn.Release();
        }
    }

    public void Dispose()
    {
        EndSession();
        _turn.Dispose();
    }

    private async Task<Result<TranslationResult>?> ExchangeAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        if (_turns >= MaxTurnsPerSession)
        {
            EndSession();
        }

        try
        {
            if (await RunAsync(request, cancellationToken) is { } result)
            {
                _turns++;
                if (result.IsFailure)
                {
                    EndSession();
                }

                return result;
            }
        }
        catch (IOException)
        {
        }

        EndSession();
        return null;
    }

    private async Task<Result<TranslationResult>?> RunAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            _session = new CodexConnection(startProcess());
            if (await OpenThreadAsync(_session, _sessionOptions.Model, cancellationToken) is not { } opened)
            {
                return null;
            }

            if (opened.IsFailure)
            {
                return opened.Error;
            }

            _threadId = opened.Value;
        }

        return await TurnAsync(_session, _threadId!, request, _sessionOptions.Effort, cancellationToken);
    }

    private static async Task<Result<string>?> OpenThreadAsync(CodexConnection session, string model, CancellationToken cancellationToken)
    {
        if (await session.HandshakeAsync(TranslationErrors.CodexFailed, cancellationToken) is not { } handshake)
        {
            return null;
        }

        if (handshake.IsFailure)
        {
            return handshake.Error;
        }

        if (await session.CallAsync(CodexCliProtocol.ReadAccount, TranslationErrors.CodexFailed, cancellationToken) is not { } account)
        {
            return null;
        }

        if (account.IsFailure)
        {
            return account.Error;
        }

        if (!CodexCliProtocol.Account(account.Value).SignedIn)
        {
            return TranslationErrors.CodexSignedOut;
        }

        var workingDirectory = Path.GetTempPath();
        if (await session.CallAsync(id => CodexCliProtocol.StartThread(id, workingDirectory, model), TranslationErrors.CodexFailed, cancellationToken)
            is not { } thread)
        {
            return null;
        }

        if (thread.IsFailure)
        {
            return thread.Error;
        }

        return CodexCliProtocol.ThreadId(thread.Value) is { } threadId ? threadId : TranslationErrors.CodexFailed(null);
    }

    private static async Task<Result<TranslationResult>?> TurnAsync(
        CodexConnection session,
        string threadId,
        TranslationRequest request,
        string effort,
        CancellationToken cancellationToken)
    {
        var requestId = session.NextRequestId();
        await session.SendAsync(CodexCliProtocol.StartTurn(requestId, threadId, request, effort), cancellationToken);

        string? turnId = null;
        var output = new StringBuilder();

        while (await session.ReadAsync(cancellationToken) is { } message)
        {
            switch (message)
            {
                case CodexMessage.Reply reply when reply.Id == requestId:
                    turnId = CodexCliProtocol.TurnId(reply.Result);
                    break;

                case CodexMessage.Failure failure when failure.Id == requestId:
                    return TranslationErrors.CodexFailed(failure.Detail);

                case CodexMessage.Delta delta when delta.ThreadId == threadId && (turnId is null || delta.TurnId == turnId):
                    output.Append(delta.Text);
                    break;

                case CodexMessage.TurnCompleted completed when completed.ThreadId == threadId && (turnId is null || completed.TurnId == turnId):
                    return completed.Succeeded
                        ? CodexCliProtocol.Translation(output.ToString(), request)
                        : TranslationErrors.CodexFailed(completed.Detail);
            }
        }

        return null;
    }

    private void EndSession()
    {
        _session?.Dispose();
        _session = null;
        _threadId = null;
        _turns = 0;
    }
}
