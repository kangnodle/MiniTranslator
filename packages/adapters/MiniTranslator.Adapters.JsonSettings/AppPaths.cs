namespace MiniTranslator.Adapters.JsonSettings;

public sealed record AppPaths(string Root)
{
    public static AppPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MiniTranslator"));

    public string SettingsFile => Path.Combine(Root, "settings.json");
}
