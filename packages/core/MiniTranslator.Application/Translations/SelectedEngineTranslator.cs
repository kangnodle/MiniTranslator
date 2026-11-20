using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Application.Translations;

public sealed class SelectedEngineTranslator : ITranslator, IDisposable
{
    private readonly SettingsStore _settings;
    private readonly Func<TranslationEngine, ITranslator> _engines;
    private readonly Lock _gate = new();
    private readonly Dictionary<TranslationEngine, ITranslator> _resolved = [];
    private AppSettings _lastSettings;
    private bool _disposed;

    public SelectedEngineTranslator(SettingsStore settings, Func<TranslationEngine, ITranslator> engines)
    {
        _settings = settings;
        _engines = engines;
        _lastSettings = settings.Current;
        _settings.Changed += OnSettingsChanged;
    }

    public Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default) =>
        Resolve(_settings.Current.Engine).TranslateAsync(request, cancellationToken);

    public Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        IProgress<string>? onPartialText,
        CancellationToken cancellationToken = default) =>
        Resolve(_settings.Current.Engine).TranslateAsync(request, onPartialText, cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
    }

    private ITranslator Resolve(TranslationEngine engine)
    {
        lock (_gate)
        {
            if (!_resolved.TryGetValue(engine, out var translator))
            {
                translator = _engines(engine);
                _resolved[engine] = translator;
            }

            return translator;
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings current)
    {
        ITranslatorSession? toRelease = null;
        ITranslatorSession? secondaryToRelease = null;

        lock (_gate)
        {
            var previous = _lastSettings;
            _lastSettings = current;

            if (previous.Engine != current.Engine
                && _resolved.TryGetValue(previous.Engine, out var previousEngine)
                && previousEngine is ITranslatorSession previousSession)
            {
                toRelease = previousSession;
            }

            if (previous.Claude != current.Claude
                && _resolved.TryGetValue(TranslationEngine.Claude, out var claude)
                && claude is ITranslatorSession claudeSession
                && !ReferenceEquals(claudeSession, toRelease))
            {
                secondaryToRelease = claudeSession;
            }
            else if ((previous.Codex != current.Codex
                      || !string.Equals(previous.CodexExecutablePath, current.CodexExecutablePath, StringComparison.Ordinal))
                     && _resolved.TryGetValue(TranslationEngine.Codex, out var codex)
                     && codex is ITranslatorSession codexSession
                     && !ReferenceEquals(codexSession, toRelease))
            {
                secondaryToRelease = codexSession;
            }
        }

        ReleaseInBackground(toRelease);
        ReleaseInBackground(secondaryToRelease);
    }

    private static void ReleaseInBackground(ITranslatorSession? session)
    {
        if (session is null)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await session.ReleaseSessionAsync();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        });
    }
}
