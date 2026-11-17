using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Application.Translations;

/// <summary>
/// Sends each request to the engine chosen in the settings at that moment.
/// Engines are resolved on first use, so one that is never selected is never started.
/// </summary>
public sealed class SelectedEngineTranslator(SettingsStore settings, Func<TranslationEngine, ITranslator> engines) : ITranslator
{
    public Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default) =>
        engines(settings.Current.Engine).TranslateAsync(request, cancellationToken);
}
