using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;

namespace MiniTranslator.Domain.Settings;

/// <summary>How the app reacts when the global hotkey is pressed.</summary>
/// <param name="CaptureSelectionOnHotkey">Copy the text selected in the foreground app.</param>
/// <param name="HideOnDeactivate">Hide the translator window when it loses focus.</param>
/// <param name="AutoTranslateOnCapture">Automatically send captured text to translation without user confirmation.</param>
public sealed record BehaviorSettings(
    bool CaptureSelectionOnHotkey,
    bool HideOnDeactivate,
    bool AutoTranslateOnCapture = false)
{
    public static BehaviorSettings Default { get; } = new(
        CaptureSelectionOnHotkey: true,
        HideOnDeactivate: true,
        AutoTranslateOnCapture: false);
}

/// <summary>
/// Immutable application settings and pure rules for applying edits.
/// </summary>
public sealed record AppSettings(
    HotkeyGesture Hotkey,
    LanguagePair Languages,
    BehaviorSettings Behavior)
{
    public AppTheme Theme { get; init; } = AppTheme.System;
    public UiLanguage UiLanguage { get; init; } = UiLanguage.Korean;

    /// <summary>Only edited fields win; concurrent changes and the immediately saved theme are preserved.</summary>
    public Result<AppSettings> MergeDraft(AppSettings draft, AppSettings original)
    {
        if (!Enum.IsDefined(draft.UiLanguage))
        {
            return SettingsErrors.InvalidUiLanguage;
        }

        return this with
        {
            Hotkey = draft.Hotkey == original.Hotkey ? Hotkey : draft.Hotkey,
            Languages = draft.Languages == original.Languages ? Languages : draft.Languages,
            UiLanguage = draft.UiLanguage == original.UiLanguage ? UiLanguage : draft.UiLanguage,
            Behavior = new BehaviorSettings(
                draft.Behavior.CaptureSelectionOnHotkey == original.Behavior.CaptureSelectionOnHotkey
                    ? Behavior.CaptureSelectionOnHotkey : draft.Behavior.CaptureSelectionOnHotkey,
                draft.Behavior.HideOnDeactivate == original.Behavior.HideOnDeactivate
                    ? Behavior.HideOnDeactivate : draft.Behavior.HideOnDeactivate,
                draft.Behavior.AutoTranslateOnCapture == original.Behavior.AutoTranslateOnCapture
                    ? Behavior.AutoTranslateOnCapture : draft.Behavior.AutoTranslateOnCapture),
        };
    }

    public Result<AppSettings> WithTheme(AppTheme theme) =>
        Enum.IsDefined(theme) ? this with { Theme = theme } : SettingsErrors.InvalidTheme;

    /// <summary>Ctrl + Space, English → Korean.</summary>
    public static AppSettings Default { get; } =
        new(HotkeyGesture.Default, LanguagePair.Default, BehaviorSettings.Default);
}
