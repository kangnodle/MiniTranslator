using System.Text.Encodings.Web;
using System.Text.Json;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.JsonSettings;

public sealed class JsonSettingsRepository(AppPaths paths) : ISettingsRepository
{
    internal const long MaxSettingsFileBytes = 64 * 1024;

    private static readonly SettingsJsonContext JsonContext = new(new JsonSerializerOptions
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.SettingsFile))
        {
            return AppSettings.Default;
        }

        try
        {
            var info = new FileInfo(paths.SettingsFile);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length > MaxSettingsFileBytes)
            {
                return AppSettings.Default;
            }

            await using var stream = File.OpenRead(paths.SettingsFile);
            var document = await JsonSerializer.DeserializeAsync(stream, JsonContext.SettingsDocument, cancellationToken);
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
        var tempFile = $"{paths.SettingsFile}.{Guid.NewGuid():N}.tmp";

        try
        {
            Directory.CreateDirectory(paths.Root);

            if ((File.GetAttributes(paths.Root) & FileAttributes.ReparsePoint) != 0
                || (File.Exists(paths.SettingsFile) && (File.GetAttributes(paths.SettingsFile) & FileAttributes.ReparsePoint) != 0))
            {
                return SettingsErrors.SaveFailed("Settings path cannot be a reparse point.");
            }

            await using (var stream = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonContext.SettingsDocument, cancellationToken);
            }

            File.Move(tempFile, paths.SettingsFile, overwrite: true);
            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryDeleteTemp(tempFile);
            return SettingsErrors.SaveFailed(ex.Message);
        }
        catch
        {
            TryDeleteTemp(tempFile);
            throw;
        }
    }

    private static void TryDeleteTemp(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
