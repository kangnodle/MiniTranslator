using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Application.UseCases;

public sealed class ChangeEngineUseCase(SettingsStore settings)
{
    public Task<Result<AppSettings>> ExecuteAsync(
        TranslationEngine engine,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(engine))
        {
            return Task.FromResult<Result<AppSettings>>(SettingsErrors.InvalidEngine);
        }

        return settings.UpdateAsync(
            current => current.WithEngineSelection(engine, model).Value,
            cancellationToken);
    }
}
