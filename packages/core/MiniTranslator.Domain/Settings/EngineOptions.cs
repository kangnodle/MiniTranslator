namespace MiniTranslator.Domain.Settings;

public sealed record EngineOptions(string Model, string Effort)
{
    public static EngineOptions Default { get; } = new(string.Empty, string.Empty);

    public static EngineOptions Create(string? model, string? effort) =>
        new((model ?? string.Empty).Trim(), (effort ?? string.Empty).Trim());
}
