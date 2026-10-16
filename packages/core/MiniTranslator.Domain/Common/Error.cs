namespace MiniTranslator.Domain.Common;

/// <summary>
/// Describes why an operation failed. Errors are values, not exceptions,
/// so pure code can report failures without side effects.
/// </summary>
/// <param name="Code">Stable, machine-readable identifier (e.g. "Hotkey.ModifierRequired").</param>
/// <param name="Message">Human-readable message suitable for display.</param>
public sealed record Error(string Code, string Message)
{
    /// <summary>Value to insert when the presentation layer localizes this error.</summary>
    public string? Parameter { get; init; }

    public static Error None { get; } = new(string.Empty, string.Empty);

    public Error WithDetail(string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? this : this with { Message = $"{Message}\n{detail}" };

    public override string ToString() => $"{Code}: {Message}";
}
