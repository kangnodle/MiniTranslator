using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Settings;

public sealed record EngineOptions(string Model, string Effort)
{
    public const int MaxModelLength = 128;

    public const int MaxEffortLength = 32;

    public static EngineOptions Default { get; } = new(string.Empty, string.Empty);

    public static EngineOptions Create(string? model, string? effort) =>
        new((model ?? string.Empty).Trim(), (effort ?? string.Empty).Trim());

    public static Result<EngineOptions> TryCreate(string? model, string? effort)
    {
        var normalized = Create(model, effort);
        if (!IsValidModel(normalized.Model))
        {
            return SettingsErrors.InvalidModel;
        }

        if (!IsValidEffort(normalized.Effort))
        {
            return SettingsErrors.InvalidEffort;
        }

        return normalized;
    }

    public static EngineOptions CreateSanitized(string? model, string? effort)
    {
        var normalized = Create(model, effort);
        return new(
            IsValidModel(normalized.Model) ? normalized.Model : string.Empty,
            IsValidEffort(normalized.Effort) ? normalized.Effort : string.Empty);
    }

    public static bool IsValidModel(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (trimmed.Length > MaxModelLength || !char.IsAsciiLetterOrDigit(trimmed[0]))
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('.' or '_' or ':' or '/' or '-'))
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsValidEffort(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return true;
        }

        if (trimmed.Length > MaxEffortLength || !char.IsAsciiLetterOrDigit(trimmed[0]))
        {
            return false;
        }

        foreach (var ch in trimmed)
        {
            if (!char.IsAsciiLetterOrDigit(ch) && ch is not ('_' or '-'))
            {
                return false;
            }
        }

        return true;
    }
}
