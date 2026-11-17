namespace MiniTranslator.Adapters.JsonSettings;

/// <summary>On-disk JSON shape of the settings. Kept separate from the domain model so either can evolve.</summary>
internal sealed class SettingsDocument
{
    public int Version { get; set; } = 1;

    public string? Hotkey { get; set; }

    public string? SourceLanguage { get; set; }

    public string? TargetLanguage { get; set; }

    public string? Theme { get; set; }
    public string? UiLanguage { get; set; }

    public string? Engine { get; set; }

    public string? CodexExecutablePath { get; set; }

    public BehaviorDocument? Behavior { get; set; }

    internal sealed class BehaviorDocument
    {
        public bool? CaptureSelectionOnHotkey { get; set; }

        public bool? HideOnDeactivate { get; set; }

        public bool? AutoTranslateOnCapture { get; set; }
    }
}
