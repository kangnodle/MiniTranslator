using System.ComponentModel;
using System.Text;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.ClaudeCli;

internal sealed class ClaudeCliTranslator : ITranslator, ITranslatorSession, IDisposable
{
    public const int MaxTurnsPerSession = 20;

    public static readonly TimeSpan DefaultIdleTimeout = TimeSpan.FromMinutes(3);

    private readonly Func<EngineOptions, IClaudeProcess> _startProcess;
    private readonly TimeSpan _timeout;
    private readonly Func<EngineOptions>? _currentOptions;
    private readonly TimeSpan? _idleTimeout;
    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly Timer? _idleTimer;

    private IClaudeProcess? _session;
    private EngineOptions _sessionOptions = EngineOptions.Default;
    private int _turns;
    private bool _disposed;

    public ClaudeCliTranslator(
        Func<EngineOptions, IClaudeProcess> startProcess,
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

            return result ?? TranslationErrors.ClaudeFailed(null);
        }
        catch (Win32Exception)
        {
            return TranslationErrors.ClaudeUnavailable;
        }
        catch (OperationCanceledException)
        {
            EndSession();
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return TranslationErrors.ClaudeTimedOut;
        }
        finally
        {
            ArmIdleTimerIfActive();
            _turn.Release();
        }
    }

    public async Task ReleaseSessionAsync(CancellationToken cancellationToken = default)
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
            _session ??= _startProcess(_sessionOptions);
            await _session.WriteLineAsync(ClaudeCliProtocol.BuildMessage(request), cancellationToken);

            var partial = onPartialText is null ? null : new StringBuilder();

            while (await _session.ReadLineAsync(cancellationToken) is { } line)
            {
                if (partial is not null && ClaudeCliProtocol.TryParseDelta(line) is { Length: > 0 } delta)
                {
                    partial.Append(delta);
                    onPartialText!.Report(partial.ToString());
                    continue;
                }

                if (ClaudeCliProtocol.ParseLine(line, request) is not { } result)
                {
                    continue;
                }

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
