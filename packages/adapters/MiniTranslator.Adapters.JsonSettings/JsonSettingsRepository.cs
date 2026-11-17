using System.Text.Encodings.Web;
using System.Text.Json;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.JsonSettings;

public sealed class JsonSettingsRepository(AppPaths paths) : ISettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.SettingsFile))
        {
            return AppSettings.Default;
        }

        try
        {
            await using var stream = File.OpenRead(paths.SettingsFile);
            var document = await JsonSerializer.DeserializeAsync<SettingsDocument>(stream, JsonOptions, cancellationToken);
            return SettingsMapper.FromDocument(document);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return AppSettings.Default;
        }
    }

    public async Task<Result<Unit>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var document = SettingsMapper.ToDocument(settings);
        var tempFile = paths.SettingsFile + ".tmp";

        try
        {
            Directory.CreateDirectory(paths.Root);

            await using (var stream = File.Create(tempFile))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
            }

            File.Move(tempFile, paths.SettingsFile, overwrite: true);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return SettingsErrors.SaveFailed(ex.Message);
        }
    }
}
