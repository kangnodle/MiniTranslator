using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Domain.Tests;

public class AppSettingsTests
{
    [Fact]
    public void An_untouched_draft_preserves_all_changes_saved_elsewhere()
    {
        var original = AppSettings.Default;
        var current = original with
        {
            Hotkey = HotkeyGesture.Parse("Ctrl+Alt+T").Value,
            Languages = LanguagePair.Create("ja", "en").Value,
            UiLanguage = UiLanguage.English,
            Font = AppFont.Batang,
            Theme = AppTheme.Dark,
            Behavior = new(false, false),
        };

        var merged = current.MergeDraft(original, original);

        Assert.Equal(current, merged.Value);
        Assert.Equal(AppSettings.Default, original);
    }

    [Fact]
    public void Edited_fields_win_while_the_current_theme_and_untouched_behavior_survive()
    {
        var original = AppSettings.Default;
        var current = original with { Theme = AppTheme.Dark, Behavior = new(false, true) };
        var draft = original with
        {
            Hotkey = HotkeyGesture.Parse("Ctrl+Alt+T").Value,
            Languages = LanguagePair.Create("en", "ja").Value,
            UiLanguage = UiLanguage.English,
            Font = AppFont.Gulim,
            Theme = AppTheme.Light,
            Behavior = new(true, false),
        };

        var merged = current.MergeDraft(draft, original).Value;

        Assert.Equal(draft.Hotkey, merged.Hotkey);
        Assert.Equal(draft.Languages, merged.Languages);
        Assert.Equal(draft.UiLanguage, merged.UiLanguage);
        Assert.Equal(draft.Font, merged.Font);
        Assert.Equal(AppTheme.Dark, merged.Theme);
        Assert.Equal(new BehaviorSettings(false, false), merged.Behavior);
        Assert.Equal(new BehaviorSettings(false, true), current.Behavior);
    }

    [Fact]
    public void An_invalid_ui_language_is_rejected()
    {
        var result = AppSettings.Default.MergeDraft(
            AppSettings.Default with { UiLanguage = (UiLanguage)99 }, AppSettings.Default);

        Assert.Equal(SettingsErrors.InvalidUiLanguage, result.Error);
    }

    [Fact]
    public void An_invalid_font_is_rejected()
    {
        var result = AppSettings.Default.MergeDraft(
            AppSettings.Default with { Font = (AppFont)99 }, AppSettings.Default);

        Assert.Equal(SettingsErrors.InvalidFont, result.Error);
    }

    [Fact]
    public void An_invalid_engine_is_rejected()
    {
        var result = AppSettings.Default.MergeDraft(
            AppSettings.Default with { Engine = (TranslationEngine)99 }, AppSettings.Default);

        Assert.Equal(SettingsErrors.InvalidEngine, result.Error);
    }

    [Fact]
    public void Captured_text_is_translated_immediately_by_default()
    {
        Assert.True(AppSettings.Default.Behavior.AutoTranslateOnCapture);
    }

    [Fact]
    public void Claude_is_the_default_engine()
    {
        Assert.Equal(TranslationEngine.Claude, AppSettings.Default.Engine);
    }

    [Fact]
    public void An_edited_engine_wins_and_an_untouched_one_keeps_the_value_saved_elsewhere()
    {
        var original = AppSettings.Default;
        var current = original with { Engine = TranslationEngine.Codex };

        Assert.Equal(TranslationEngine.Codex, current.MergeDraft(original, original).Value.Engine);
        Assert.Equal(TranslationEngine.Codex,
            original.MergeDraft(original with { Engine = TranslationEngine.Codex }, original).Value.Engine);
    }

    [Fact]
    public void An_edited_codex_path_wins_and_an_untouched_one_keeps_the_value_saved_elsewhere()
    {
        var original = AppSettings.Default;
        var current = original with { CodexExecutablePath = @"C:\saved\codex.exe" };

        Assert.Equal(@"C:\saved\codex.exe", current.MergeDraft(original, original).Value.CodexExecutablePath);
        Assert.Equal(@"C:\edited\codex.exe",
            current.MergeDraft(original with { CodexExecutablePath = @"C:\edited\codex.exe" }, original).Value.CodexExecutablePath);
    }

    [Theory]
    [InlineData(@"  C:\Tools\codex.exe  ", @"C:\Tools\codex.exe")]
    [InlineData(@"""C:\Program Files\Codex\codex.exe"" ", @"C:\Program Files\Codex\codex.exe")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void A_pasted_codex_path_loses_its_quotes_and_padding(string? typed, string expected)
    {
        Assert.Equal(expected, AppSettings.NormalizePath(typed));
    }

    [Fact]
    public void An_edited_codex_path_is_normalized_when_merged()
    {
        var draft = AppSettings.Default with { CodexExecutablePath = @" ""C:\Tools\codex.exe"" " };

        Assert.Equal(@"C:\Tools\codex.exe", AppSettings.Default.MergeDraft(draft, AppSettings.Default).Value.CodexExecutablePath);
    }

    [Fact]
    public void An_invalid_theme_is_rejected()
    {
        Assert.Equal(SettingsErrors.InvalidTheme, AppSettings.Default.WithTheme((AppTheme)99).Error);
    }

    [Theory]
    [InlineData(AppTheme.System)]
    [InlineData(AppTheme.Light)]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.TomorrowNightBlue)]
    [InlineData(AppTheme.SolarizedLight)]
    [InlineData(AppTheme.SolarizedDark)]
    public void Changing_the_theme_preserves_the_other_settings(AppTheme theme)
    {
        var current = AppSettings.Default with { UiLanguage = UiLanguage.English, Behavior = new(false, false) };

        Assert.Equal(current with { Theme = theme }, current.WithTheme(theme).Value);
    }

    [Fact]
    public void Selecting_an_engine_and_model_updates_active_display_and_preserves_effort()
    {
        var current = AppSettings.Default with { Claude = EngineOptions.Create("sonnet", "high") };

        var updated = current.WithEngineSelection(TranslationEngine.Claude, "opus").Value;

        Assert.Equal(TranslationEngine.Claude, updated.Engine);
        Assert.Equal("opus", updated.ActiveModelDisplay);
        Assert.Equal("high", updated.Claude.Effort);

        var codex = updated.WithEngineSelection(TranslationEngine.Codex, "gpt-6.1-terra").Value;

        Assert.Equal(TranslationEngine.Codex, codex.Engine);
        Assert.Equal("gpt-6.1-terra", codex.ActiveModelDisplay);
    }

    [Fact]
    public void Selecting_an_invalid_engine_is_rejected()
    {
        Assert.Equal(SettingsErrors.InvalidEngine, AppSettings.Default.WithEngineSelection((TranslationEngine)99).Error);
    }

    [Theory]
    [InlineData(@"\\attacker\share\codex.exe")]
    [InlineData(@"\\?\C:\Tools\codex.exe")]
    [InlineData(@"codex.exe")]
    [InlineData(@"C:\Tools\..\Windows\cmd.exe")]
    [InlineData(@"C:\Tools\codex.cmd")]
    [InlineData(@"C:\.exe")]
    public void Unsafe_codex_executable_paths_are_rejected_on_merge(string path)
    {
        var draft = AppSettings.Default with { CodexExecutablePath = path };

        Assert.Equal(SettingsErrors.InvalidCodexExecutablePath, AppSettings.Default.MergeDraft(draft, AppSettings.Default).Error);
    }

    [Theory]
    [InlineData("--dangerously-skip-permissions")]
    [InlineData("sonnet --tools bash")]
    [InlineData("sonnet\n--flag")]
    public void Unsafe_model_values_are_rejected(string model)
    {
        var draft = AppSettings.Default with { Claude = EngineOptions.Create(model, "") };

        Assert.Equal(SettingsErrors.InvalidModel, AppSettings.Default.MergeDraft(draft, AppSettings.Default).Error);
        Assert.Equal(SettingsErrors.InvalidModel, AppSettings.Default.WithEngineSelection(TranslationEngine.Claude, model).Error);
    }

    [Theory]
    [InlineData("--flag")]
    [InlineData("high --extra")]
    [InlineData("high\nlow")]
    public void Unsafe_effort_values_are_rejected(string effort)
    {
        var draft = AppSettings.Default with { Codex = EngineOptions.Create("", effort) };

        Assert.Equal(SettingsErrors.InvalidEffort, AppSettings.Default.MergeDraft(draft, AppSettings.Default).Error);
    }
}
