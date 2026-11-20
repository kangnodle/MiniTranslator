using System.ComponentModel;
using System.Text;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.CodexCli;

internal sealed class CodexCliTranslator : ITranslator, ITranslatorSession, IDisposable
{
    public const int MaxTurnsPerSession = 20;

    internal const int MaxOutputLength = 100_000;

    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(3);

    private readonly Func<ICodexProcess> _startProcess;
    private readonly TimeSpan _timeout;
    private readonly Func<EngineOptions>? _currentOptions;
    private readonly TimeSpan? _idleTimeout;
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly Timer? _idleTimer;

    private CodexConnection? _session;
    private int _turns;
    private EngineOptions _sessionOptions = EngineOptions.Default;
    private bool _disposed;

    public CodexCliTranslator(
        Func<ICodexProcess> startProcess,
        TimeSpan timeout,
        Func<EngineOptions>? currentOptions = null,
        TimeSpan? idleTimeout = null)
    {
        _startProcess = startProcess;
        _timeout = timeout;
        _currentOptions = currentOptions;
        _idleTimeout = idleTimeout;
        if (idleTimeout is { } delay && delay > TimeSpan.Zero)
        {
            _idleTimer = new Timer(OnIdleTimeout, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default) =>
        TranslateAsync(request, onPartialText: null, cancellationToken);

    public async Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        IProgress<string>? onPartialText,
        CancellationToken cancellationToken = default)
    {
        await _turn.WaitAsync(cancellationToken);
        try
        {
            DisarmIdleTimer();

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(_timeout);

            var options = _currentOptions?.Invoke() ?? EngineOptions.Default;
            if (options != _sessionOptions)
            {
                EndSession();
                _sessionOptions = options;
            }

            var reusedSession = _session is not null && _turns < MaxTurnsPerSession;
            var result = await ExchangeAsync(request, onPartialText, deadline.Token);

            if (result is null && reusedSession)
            {
                result = await ExchangeAsync(request, onPartialText, deadline.Token);
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
            ArmIdleTimerIfActive();
            _turn.Release();
        }
    }

    public Task ReleaseSessionAsync(CancellationToken cancellationToken = default) =>
        ResetAsync(cancellationToken);

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed))
        {
            return;
        }

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
        if (Interlocked.Exchange(ref _disposed, true))
        {
            return;
        }

        _idleTimer?.Dispose();
        EndSession();
        _turn.Dispose();
    }

    private async Task<Result<TranslationResult>?> ExchangeAsync(
        TranslationRequest request,
        IProgress<string>? onPartialText,
        CancellationToken cancellationToken)
    {
        if (_turns >= MaxTurnsPerSession)
        {
            EndSession();
        }

        try
        {
            if (await RunAsync(request, onPartialText, cancellationToken) is { } result)
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

    private async Task<Result<TranslationResult>?> RunAsync(
        TranslationRequest request,
        IProgress<string>? onPartialText,
        CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            _session = new CodexConnection(_startProcess());
            if (await OpenSessionAsync(_session, cancellationToken) is not { } opened)
            {
                return null;
            }

            if (opened.IsFailure)
            {
                return opened.Error;
            }
        }

        if (await StartThreadAsync(_session, _sessionOptions.Model, cancellationToken) is not { } thread)
        {
            return null;
        }

        if (thread.IsFailure)
        {
            return thread.Error;
        }

        return await TurnAsync(_session, thread.Value, request, _sessionOptions.Effort, onPartialText, cancellationToken);
    }

    private static async Task<Result<Unit>?> OpenSessionAsync(CodexConnection session, CancellationToken cancellationToken)
    {
        if (await session.HandshakeAsync(TranslationErrors.CodexFailed, cancellationToken) is not { } handshake)
        {
            return null;
        }

        if (handshake.IsFailure)
        {
            return handshake.Error;
        }

        if (await session.CallAsync(id => CodexCliProtocol.ReadAccount(id, refreshToken: false), TranslationErrors.CodexFailed, cancellationToken) is not { } account)
        {
            return null;
        }

        if (account.IsFailure)
        {
            return account.Error;
        }

        return CodexCliProtocol.Account(account.Value).SignedIn
            ? Result.Success()
            : TranslationErrors.CodexSignedOut;
    }

    private static async Task<Result<string>?> StartThreadAsync(CodexConnection session, string model, CancellationToken cancellationToken)
    {
        var workingDirectory = session.WorkingDirectory;
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
        IProgress<string>? onPartialText,
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
                    if (output.Length + delta.Text.Length > MaxOutputLength)
                    {
                        return TranslationErrors.CodexFailed("Translation output exceeded the maximum allowed length.");
                    }

                    output.Append(delta.Text);
                    onPartialText?.Report(output.ToString());
                    break;

                case CodexMessage.TurnCompleted completed when completed.ThreadId == threadId && (turnId is null || completed.TurnId == turnId):
                    return completed.Succeeded
                        ? CodexCliProtocol.Translation(output.ToString(), request)
                        : TranslationErrors.CodexFailed(completed.Detail);
            }
        }

        return null;
    }

    private void OnIdleTimeout(object? state)
    {
        if (Volatile.Read(ref _disposed))
        {
            return;
        }

        try
        {
            if (!_turn.Wait(0))
            {
                return;
            }
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (!Volatile.Read(ref _disposed))
            {
                EndSession();
            }
        }
        finally
        {
            try
            {
                _turn.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    private void DisarmIdleTimer()
    {
        if (_idleTimer is null || Volatile.Read(ref _disposed))
        {
            return;
        }

        try
        {
            _idleTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ArmIdleTimerIfActive()
    {
        if (_idleTimer is null || _idleTimeout is not { } delay || _session is null || Volatile.Read(ref _disposed))
        {
            return;
        }

        try
        {
            _idleTimer.Change(delay, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void EndSession()
    {
        DisarmIdleTimer();
        _session?.Dispose();
        _session = null;
        _turns = 0;
    }
}
