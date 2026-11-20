using System.Text;

namespace MiniTranslator.Domain.Common;

public sealed record Error(string Code, string Message)
{
    public const int MaxDetailLength = 1000;

    public string? Parameter { get; init; }

    public static Error None { get; } = new(string.Empty, string.Empty);

    public Error WithDetail(string? detail)
    {
        var sanitized = SanitizeDetail(detail);
        return sanitized.Length == 0 ? this : this with { Message = $"{Message}\n{sanitized}" };
    }

    public override string ToString() => $"{Code}: {Message}";

    private static string SanitizeDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return string.Empty;
        }

        var trimmed = detail.Trim();
        var builder = new StringBuilder(Math.Min(trimmed.Length, MaxDetailLength + 3));
        var truncated = false;

        foreach (var ch in trimmed)
        {
            if (IsDisallowedCharacter(ch))
            {
                continue;
            }

            if (builder.Length >= MaxDetailLength)
            {
                truncated = true;
                break;
            }

            builder.Append(ch);
        }

        var result = builder.ToString().Trim();
        if (result.Length == 0)
        {
            return string.Empty;
        }

        return truncated ? result + "..." : result;
    }

    private static bool IsDisallowedCharacter(char ch) =>
        (char.IsControl(ch) && ch is not ('\n' or '\r' or '\t'))
        || ch is '\u200E' or '\u200F' or >= '\u202A' and <= '\u202E' or >= '\u2066' and <= '\u2069';
}
