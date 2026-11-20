using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;

namespace MiniTranslator.Domain.Settings;

public sealed record BehaviorSettings(
    bool CaptureSelectionOnHotkey,
    bool HideOnDeactivate,
    bool AutoTranslateOnCapture = true)
{
    public static BehaviorSettings Default { get; } = new(
        CaptureSelectionOnHotkey: true,
        HideOnDeactivate: true,
        AutoTranslateOnCapture: true);
}

public sealed record AppSettings(
    HotkeyGesture Hotkey,
    LanguagePair Languages,
    BehaviorSettings Behavior)
{
    public AppTheme Theme { get; init; } = AppTheme.System;
    public UiLanguage UiLanguage { get; init; } = UiLanguage.Korean;
    public AppFont Font { get; init; } = AppFont.System;
    public TranslationEngine Engine { get; init; } = TranslationEngine.Claude;

    public string CodexExecutablePath { get; init; } = string.Empty;

    public EngineOptions Claude { get; init; } = EngineOptions.Default;

    public EngineOptions Codex { get; init; } = EngineOptions.Default;

    public Result<AppSettings> MergeDraft(AppSettings draft, AppSettings original)
    {
        if (!Enum.IsDefined(draft.UiLanguage))
        {
            return SettingsErrors.InvalidUiLanguage;
        }

        if (!Enum.IsDefined(draft.Font))
        {
            return SettingsErrors.InvalidFont;
        }

        if (!Enum.IsDefined(draft.Engine))
        {
            return SettingsErrors.InvalidEngine;
        }

        var codexPath = CodexExecutablePath;
        if (draft.CodexExecutablePath != original.CodexExecutablePath)
        {
            codexPath = NormalizePath(draft.CodexExecutablePath);
            if (!IsValidExecutablePath(codexPath))
            {
                return SettingsErrors.InvalidCodexExecutablePath;
            }
        }

        var claudeOptions = Claude;
        if (draft.Claude != original.Claude)
        {
            var validated = EngineOptions.TryCreate(draft.Claude.Model, draft.Claude.Effort);
            if (validated.IsFailure)
            {
                return validated.Error;
            }

            claudeOptions = validated.Value;
        }

        var codexOptions = Codex;
        if (draft.Codex != original.Codex)
        {
            var validated = EngineOptions.TryCreate(draft.Codex.Model, draft.Codex.Effort);
            if (validated.IsFailure)
            {
                return validated.Error;
            }

            codexOptions = validated.Value;
        }

        return this with
        {
            Hotkey = draft.Hotkey == original.Hotkey ? Hotkey : draft.Hotkey,
            Languages = draft.Languages == original.Languages ? Languages : draft.Languages,
            UiLanguage = draft.UiLanguage == original.UiLanguage ? UiLanguage : draft.UiLanguage,
            Font = draft.Font == original.Font ? Font : draft.Font,
            Engine = draft.Engine == original.Engine ? Engine : draft.Engine,
            CodexExecutablePath = codexPath,
            Claude = claudeOptions,
            Codex = codexOptions,
            Behavior = new BehaviorSettings(
                draft.Behavior.CaptureSelectionOnHotkey == original.Behavior.CaptureSelectionOnHotkey
                    ? Behavior.CaptureSelectionOnHotkey : draft.Behavior.CaptureSelectionOnHotkey,
                draft.Behavior.HideOnDeactivate == original.Behavior.HideOnDeactivate
                    ? Behavior.HideOnDeactivate : draft.Behavior.HideOnDeactivate,
                draft.Behavior.AutoTranslateOnCapture == original.Behavior.AutoTranslateOnCapture
                    ? Behavior.AutoTranslateOnCapture : draft.Behavior.AutoTranslateOnCapture),
        };
    }

    public const int MaxExecutablePathLength = 260;

    public static string NormalizePath(string? path) => (path ?? string.Empty).Trim().Trim('"').Trim();

    public static bool IsValidExecutablePath(string? path)
    {
        var normalized = NormalizePath(path);
        if (normalized.Length == 0)
        {
            return true;
        }

        if (normalized.Length > MaxExecutablePathLength
            || normalized.Length < 8
            || !char.IsAsciiLetter(normalized[0])
            || normalized[1] != ':'
            || normalized[2] is not ('\\' or '/')
            || !normalized.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        for (var i = 0; i < normalized.Length; i++)
        {
            var ch = normalized[i];
            if (char.IsControl(ch) || ch is '"' or '<' or '>' or '|' or '*' or '?' or ';' or '&')
            {
                return false;
            }
        }

        var segments = normalized[3..].Split(['\\', '/']);
        if (segments.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length == 0 || segment is "." or "..")
            {
                return false;
            }
        }

        var fileName = segments[^1];
        return fileName.Length > 4;
    }

    public Result<AppSettings> WithTheme(AppTheme theme) =>
        Enum.IsDefined(theme) ? this with { Theme = theme } : SettingsErrors.InvalidTheme;

    public Result<AppSettings> WithEngineSelection(TranslationEngine engine, string? model = null)
    {
        if (!Enum.IsDefined(engine))
        {
            return SettingsErrors.InvalidEngine;
        }

        if (model is null)
        {
            return this with { Engine = engine };
        }

        if (!EngineOptions.IsValidModel(model))
        {
            return SettingsErrors.InvalidModel;
        }

        return engine switch
        {
            TranslationEngine.Claude => this with
            {
                Engine = engine,
                Claude = EngineOptions.Create(model, Claude.Effort),
            },
            TranslationEngine.Codex => this with
            {
                Engine = engine,
                Codex = EngineOptions.Create(model, Codex.Effort),
            },
            _ => SettingsErrors.InvalidEngine,
        };
    }

    public string ActiveModelDisplay => Engine switch
    {
        TranslationEngine.Claude => Claude.Model.Length == 0 ? "sonnet" : Claude.Model,
        TranslationEngine.Codex => Codex.Model.Length == 0 ? "gpt-6.1-terra" : Codex.Model,
        _ => string.Empty,
    };

    public static AppSettings Default { get; } =
        new(HotkeyGesture.Default, LanguagePair.Default, BehaviorSettings.Default);
}
