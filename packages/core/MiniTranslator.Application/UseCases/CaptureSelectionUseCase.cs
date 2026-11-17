using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;

namespace MiniTranslator.Application.UseCases;

public sealed class CaptureSelectionUseCase(ITextSelectionReader reader, SettingsStore settings)
{
    public async Task<string?> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.Current.Behavior.CaptureSelectionOnHotkey)
        {
            return null;
        }

        var text = await reader.ReadSelectionAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
