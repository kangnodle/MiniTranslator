using System.ComponentModel;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.ClaudeCli;

internal sealed class ClaudeCliTranslator(
    Func<EngineOptions, IClaudeProcess> startProcess,
    TimeSpan timeout,
    Func<EngineOptions>? currentOptions = null) : ITranslator, IDisposable
{
    public const int MaxTurnsPerSession = 20;

    private readonly SemaphoreSlim _turn = new(1, 1);
    private IClaudeProcess? _session;
    private EngineOptions _sessionOptions = EngineOptions.Default;
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
            _session ??= startProcess(_sessionOptions);
            await _session.WriteLineAsync(ClaudeCliProtocol.BuildMessage(request), cancellationToken);

            while (await _session.ReadLineAsync(cancellationToken) is { } line)
            {
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

    private void EndSession()
    {
        _session?.Dispose();
        _session = null;
        _turns = 0;
    }
}
