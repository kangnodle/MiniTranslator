namespace MiniTranslator.Adapters.JsonSettings;

public sealed record AppPaths(string Root, string? LocalRoot = null)
{
    public static AppPaths Default { get; } = new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MiniTranslator"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MiniTranslator"));

    public string SettingsFile => Path.Combine(Root, "settings.json");

    public string CodexHome => Path.Combine(LocalRoot ?? Root, "codex");

    public string LegacyCodexHome => Path.Combine(Root, "codex");
}
