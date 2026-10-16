namespace MiniTranslator.Adapters.JsonSettings;

/// <summary>Locations of the files MiniTranslator writes.</summary>
public sealed record AppPaths(string Root)
{
    public static AppPaths Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MiniTranslator"));

    public string SettingsFile => Path.Combine(Root, "settings.json");
}
