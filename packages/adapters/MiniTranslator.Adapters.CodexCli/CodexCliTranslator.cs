using System.ComponentModel;
using System.Text;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.CodexCli;

/// <summary>
/// Translates through one long-lived Codex app-server and one thread on it, because starting the CLI and
/// starting a thread each cost seconds per request. Requests run one at a time. The session is replaced when it
/// fails, when a request is cancelled mid-turn, and every <see cref="MaxTurnsPerSession"/> translations so the
/// conversation its thread accumulates stays small.
/// </summary>
/// <param name="startProcess">Starts the app-server.</param>
/// <param name="timeout">How long one translation may take, session start included.</param>
internal sealed class CodexCliTranslator(Func<ICodexProcess> startProcess, TimeSpan timeout) : ITranslator, IDisposable
{
    public const int MaxTurnsPerSession = 20;

    private readonly SemaphoreSlim _turn = new(1, 1);
    private CodexConnection? _session;
    private string? _threadId;
    private int _turns;

    public async Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        await _turn.WaitAsync(cancellationToken);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);

            var reusedSession = _session is not null && _turns < MaxTurnsPerSession;
            var result = await ExchangeAsync(request, deadline.Token);

            // A session can die while idle; that says nothing about a fresh one, so try once more.
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
            // The turn was abandoned halfway, so the session's next output would belong to this request.
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

    /// <summary>
    /// Ends the session once any running translation has finished, so the next one starts a process that sees
    /// the current sign-in.
    /// </summary>
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

    /// <returns>The turn's outcome, or <c>null</c> when the session ended without producing one.</returns>
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
            // The process closed its pipes; handled like any other exit below.
        }

        EndSession();
        return null;
    }

    private async Task<Result<TranslationResult>?> RunAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            _session = new CodexConnection(startProcess());
            if (await OpenThreadAsync(_session, cancellationToken) is not { } opened)
            {
                return null;
            }

            if (opened.IsFailure)
            {
                return opened.Error;
            }

            _threadId = opened.Value;
        }

        return await TurnAsync(_session, _threadId!, request, cancellationToken);
    }

    /// <summary>Handshake, sign-in check, and thread start on a fresh process.</summary>
    /// <returns>The thread id or why there is none; <c>null</c> when the process ended first.</returns>
    private static async Task<Result<string>?> OpenThreadAsync(CodexConnection session, CancellationToken cancellationToken)
    {
        if (await session.HandshakeAsync(TranslationErrors.CodexFailed, cancellationToken) is not { } handshake)
        {
            return null;
        }

        if (handshake.IsFailure)
        {
            return handshake.Error;
        }

        // A signed-out server still starts threads, so this is the only place the missing sign-in shows up clearly.
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
        if (await session.CallAsync(id => CodexCliProtocol.StartThread(id, workingDirectory), TranslationErrors.CodexFailed, cancellationToken)
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

    /// <returns>The translation or why the turn failed; <c>null</c> when the process ended first.</returns>
    private static async Task<Result<TranslationResult>?> TurnAsync(
        CodexConnection session,
        string threadId,
        TranslationRequest request,
        CancellationToken cancellationToken)
    {
        var requestId = session.NextRequestId();
        await session.SendAsync(CodexCliProtocol.StartTurn(requestId, threadId, request), cancellationToken);

        // Turns on this thread never overlap, so until the reply names the turn, anything on the thread is this turn's.
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
