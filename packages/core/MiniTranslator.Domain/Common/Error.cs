namespace MiniTranslator.Domain.Common;

public sealed record Error(string Code, string Message)
{
    public string? Parameter { get; init; }

    public static Error None { get; } = new(string.Empty, string.Empty);

    public Error WithDetail(string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? this : this with { Message = $"{Message}\n{detail}" };

    public override string ToString() => $"{Code}: {Message}";
}
