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
        if (!Directory.Exists(paths.Root) || !File.Exists(paths.SettingsFile))
        {
            return AppSettings.Default;
        }

        try
        {
            if (IsReparsePoint(paths.Root) || IsReparsePoint(paths.SettingsFile))
            {
                return AppSettings.Default;
            }

            await using var stream = new FileStream(paths.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bytes = await ReadBoundedBytesAsync(stream, (int)MaxSettingsFileBytes, cancellationToken);
            if (bytes is null)
            {
                return AppSettings.Default;
            }

            var document = JsonSerializer.Deserialize(bytes, JsonContext.SettingsDocument);
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
            if (Directory.Exists(paths.Root) && IsReparsePoint(paths.Root))
            {
                return SettingsErrors.SaveFailed("Settings path cannot be a reparse point.");
            }

            Directory.CreateDirectory(paths.Root);

            if (IsReparsePoint(paths.Root)
                || (File.Exists(paths.SettingsFile) && IsReparsePoint(paths.SettingsFile)))
            {
                return SettingsErrors.SaveFailed("Settings path cannot be a reparse point.");
            }

            await using (var stream = new FileStream(tempFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonContext.SettingsDocument, cancellationToken);
            }

            if (File.Exists(paths.SettingsFile) && IsReparsePoint(paths.SettingsFile))
            {
                TryDeleteTemp(tempFile);
                return SettingsErrors.SaveFailed("Settings path cannot be a reparse point.");
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

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static async Task<byte[]?> ReadBoundedBytesAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        var total = 0;

        while (true)
        {
            var remaining = maxBytes - total + 1;
            var toRead = Math.Min(chunk.Length, remaining);
            var read = await stream.ReadAsync(chunk.AsMemory(0, toRead), cancellationToken);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            total += read;
            if (total > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
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
