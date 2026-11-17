using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.JsonSettings;

/// <summary>
/// Pure translation between <see cref="SettingsDocument"/> and <see cref="AppSettings"/>.
/// Any missing or invalid value falls back to its default, so a damaged file never blocks startup.
/// </summary>
internal static class SettingsMapper
{
    public static SettingsDocument ToDocument(AppSettings settings) => new()
    {
        Hotkey = settings.Hotkey.ToString(),
        SourceLanguage = settings.Languages.Source.Code,
        TargetLanguage = settings.Languages.Target.Code,
        Theme = settings.Theme.ToString(),
        UiLanguage = settings.UiLanguage.ToString(),
        Engine = settings.Engine.ToString(),
        CodexExecutablePath = settings.CodexExecutablePath.Length == 0 ? null : settings.CodexExecutablePath,
        Behavior = new SettingsDocument.BehaviorDocument
        {
            CaptureSelectionOnHotkey = settings.Behavior.CaptureSelectionOnHotkey,
            HideOnDeactivate = settings.Behavior.HideOnDeactivate,
            AutoTranslateOnCapture = settings.Behavior.AutoTranslateOnCapture,
        },
    };

    public static AppSettings FromDocument(SettingsDocument? document)
    {
        if (document is null)
        {
            return AppSettings.Default;
        }

        var defaults = AppSettings.Default;

        return new AppSettings(
            Hotkey: HotkeyGesture.Parse(document.Hotkey).GetValueOrDefault(defaults.Hotkey),
            Languages: LanguagePair.Create(document.SourceLanguage, document.TargetLanguage).GetValueOrDefault(defaults.Languages),
            Behavior: new BehaviorSettings(
                document.Behavior?.CaptureSelectionOnHotkey ?? defaults.Behavior.CaptureSelectionOnHotkey,
                document.Behavior?.HideOnDeactivate ?? defaults.Behavior.HideOnDeactivate,
                document.Behavior?.AutoTranslateOnCapture ?? defaults.Behavior.AutoTranslateOnCapture))
        {
            Theme = Enum.TryParse<AppTheme>(document.Theme, ignoreCase: true, out var theme) && Enum.IsDefined(theme)
                ? theme : defaults.Theme,
            UiLanguage = Enum.TryParse<UiLanguage>(document.UiLanguage, ignoreCase: true, out var uiLanguage) && Enum.IsDefined(uiLanguage)
                ? uiLanguage : defaults.UiLanguage,
            Engine = Enum.TryParse<TranslationEngine>(document.Engine, ignoreCase: true, out var engine) && Enum.IsDefined(engine)
                ? engine : defaults.Engine,
            CodexExecutablePath = AppSettings.NormalizePath(document.CodexExecutablePath),
        };
    }
}
