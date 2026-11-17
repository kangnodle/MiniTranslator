using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Application.UseCases;

public sealed class ChangeLanguagesUseCase(SettingsStore settings)
{
    public Task<Result<AppSettings>> ExecuteAsync(LanguagePair languages, CancellationToken cancellationToken = default) =>
        settings.UpdateAsync(s => s with { Languages = languages }, cancellationToken);
}
