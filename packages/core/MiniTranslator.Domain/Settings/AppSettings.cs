using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;

namespace MiniTranslator.Domain.Settings;

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

public sealed record AppSettings(
    HotkeyGesture Hotkey,
    LanguagePair Languages,
    BehaviorSettings Behavior)
{
    public AppTheme Theme { get; init; } = AppTheme.System;
    public UiLanguage UiLanguage { get; init; } = UiLanguage.Korean;
    public TranslationEngine Engine { get; init; } = TranslationEngine.Google;

    public string CodexExecutablePath { get; init; } = string.Empty;

    public Result<AppSettings> MergeDraft(AppSettings draft, AppSettings original)
    {
        if (!Enum.IsDefined(draft.UiLanguage))
        {
            return SettingsErrors.InvalidUiLanguage;
        }

        if (!Enum.IsDefined(draft.Engine))
        {
            return SettingsErrors.InvalidEngine;
        }

        return this with
        {
            Hotkey = draft.Hotkey == original.Hotkey ? Hotkey : draft.Hotkey,
            Languages = draft.Languages == original.Languages ? Languages : draft.Languages,
            UiLanguage = draft.UiLanguage == original.UiLanguage ? UiLanguage : draft.UiLanguage,
            Engine = draft.Engine == original.Engine ? Engine : draft.Engine,
            CodexExecutablePath = draft.CodexExecutablePath == original.CodexExecutablePath
                ? CodexExecutablePath : NormalizePath(draft.CodexExecutablePath),
            Behavior = new BehaviorSettings(
                draft.Behavior.CaptureSelectionOnHotkey == original.Behavior.CaptureSelectionOnHotkey
                    ? Behavior.CaptureSelectionOnHotkey : draft.Behavior.CaptureSelectionOnHotkey,
                draft.Behavior.HideOnDeactivate == original.Behavior.HideOnDeactivate
                    ? Behavior.HideOnDeactivate : draft.Behavior.HideOnDeactivate,
                draft.Behavior.AutoTranslateOnCapture == original.Behavior.AutoTranslateOnCapture
                    ? Behavior.AutoTranslateOnCapture : draft.Behavior.AutoTranslateOnCapture),
        };
    }

    public static string NormalizePath(string? path) => (path ?? string.Empty).Trim().Trim('"').Trim();

    public Result<AppSettings> WithTheme(AppTheme theme) =>
        Enum.IsDefined(theme) ? this with { Theme = theme } : SettingsErrors.InvalidTheme;

    public static AppSettings Default { get; } =
        new(HotkeyGesture.Default, LanguagePair.Default, BehaviorSettings.Default);
}
