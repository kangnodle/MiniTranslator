using System.ComponentModel;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.ClaudeCli;

/// <summary>
/// Translates through one long-lived Claude Code CLI session, because starting the CLI costs seconds per request.
/// Requests run one at a time. The session is replaced when it fails, when a request is cancelled mid-turn,
/// and every <see cref="MaxTurnsPerSession"/> translations so the conversation it accumulates stays small.
/// </summary>
internal sealed class ClaudeCliTranslator(Func<IClaudeProcess> startProcess, TimeSpan timeout) : ITranslator, IDisposable
{
    public const int MaxTurnsPerSession = 20;

    private readonly SemaphoreSlim _turn = new(1, 1);
    private IClaudeProcess? _session;
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

            return result ?? TranslationErrors.ClaudeFailed(null);
        }
        catch (Win32Exception)
        {
            return TranslationErrors.ClaudeUnavailable;
        }
        catch (OperationCanceledException)
        {
            // The turn was abandoned halfway, so the session's next output would belong to this request.
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

    /// <returns>The turn's outcome, or <c>null</c> when the session ended without producing one.</returns>
    private async Task<Result<TranslationResult>?> ExchangeAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        if (_turns >= MaxTurnsPerSession)
        {
            EndSession();
        }

        try
        {
            _session ??= startProcess();
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
            // The process closed its pipes; handled like any other exit below.
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
