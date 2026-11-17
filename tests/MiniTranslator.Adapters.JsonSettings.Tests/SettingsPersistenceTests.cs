using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.JsonSettings.Tests;

public class SettingsMapperTests
{
    private static readonly AppSettings Custom = new(
        HotkeyGesture.Parse("Ctrl+Shift+Q").Value,
        LanguagePair.Create("auto", "ja").Value,
        new BehaviorSettings(CaptureSelectionOnHotkey: false, HideOnDeactivate: false))
    { Theme = AppTheme.Dark, UiLanguage = UiLanguage.English, Engine = TranslationEngine.Codex, CodexExecutablePath = @"C:\Tools\codex.exe" };

    [Fact]
    public void Round_trips()
    {
        var document = SettingsMapper.ToDocument(Custom);
        Assert.Equal(Custom, SettingsMapper.FromDocument(document));
    }

    [Fact]
    public void Missing_document_yields_defaults()
    {
        Assert.Equal(AppSettings.Default, SettingsMapper.FromDocument(null));
    }

    [Theory]
    [InlineData("Korean", UiLanguage.Korean)]
    [InlineData("english", UiLanguage.English)]
    [InlineData(null, UiLanguage.Korean)]
    [InlineData("unknown", UiLanguage.Korean)]
    [InlineData("2", UiLanguage.Korean)]
    public void Reads_ui_language_and_defaults_older_or_invalid_values(string? value, UiLanguage expected)
    {
        Assert.Equal(expected, SettingsMapper.FromDocument(new SettingsDocument { UiLanguage = value }).UiLanguage);
    }

    [Theory]
    [InlineData("Google", TranslationEngine.Google)]
    [InlineData("claude", TranslationEngine.Claude)]
    [InlineData("Codex", TranslationEngine.Codex)]
    [InlineData(null, TranslationEngine.Google)]
    [InlineData("unknown", TranslationEngine.Google)]
    [InlineData("7", TranslationEngine.Google)]
    public void Reads_the_engine_and_defaults_older_or_invalid_values(string? value, TranslationEngine expected)
    {
        Assert.Equal(expected, SettingsMapper.FromDocument(new SettingsDocument { Engine = value }).Engine);
    }

    [Fact]
    public void Codex_path_is_empty_in_older_files_and_left_out_of_the_file_when_empty()
    {
        Assert.Equal("", SettingsMapper.FromDocument(new SettingsDocument()).CodexExecutablePath);
        Assert.Null(SettingsMapper.ToDocument(AppSettings.Default).CodexExecutablePath);
    }

    [Theory]
    [InlineData("System", AppTheme.System)]
    [InlineData("Light", AppTheme.Light)]
    [InlineData("dark", AppTheme.Dark)]
    [InlineData("TomorrowNightBlue", AppTheme.TomorrowNightBlue)]
    [InlineData("SolarizedLight", AppTheme.SolarizedLight)]
    [InlineData("solarizeddark", AppTheme.SolarizedDark)]
    [InlineData(null, AppTheme.System)]
    [InlineData("unknown", AppTheme.System)]
    [InlineData("99", AppTheme.System)]
    public void Reads_the_theme_and_defaults_missing_or_invalid_values(string? value, AppTheme expected)
    {
        Assert.Equal(expected, SettingsMapper.FromDocument(new SettingsDocument { Theme = value }).Theme);
    }

    [Fact]
    public void Invalid_values_fall_back_to_defaults_individually()
    {
        var document = new SettingsDocument
        {
            Hotkey = "Shift+A",           // invalid: shift-only
            SourceLanguage = "ko",
            TargetLanguage = "ko",        // invalid: same language
            Behavior = new SettingsDocument.BehaviorDocument { HideOnDeactivate = false },
        };

        var settings = SettingsMapper.FromDocument(document);

        Assert.Equal(HotkeyGesture.Default, settings.Hotkey);
        Assert.Equal(LanguagePair.Default, settings.Languages);
        Assert.True(settings.Behavior.CaptureSelectionOnHotkey);
        Assert.False(settings.Behavior.HideOnDeactivate);
    }
}

public sealed class JsonSettingsRepositoryTests : IDisposable
{
    private readonly AppPaths _paths = new(Path.Combine(Path.GetTempPath(), "MiniTranslator.Tests", Guid.NewGuid().ToString("N")));

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.TomorrowNightBlue)]
    [InlineData(AppTheme.SolarizedLight)]
    [InlineData(AppTheme.SolarizedDark)]
    public async Task Saves_and_loads(AppTheme theme)
    {
        var repository = new JsonSettingsRepository(_paths);
        var settings = AppSettings.Default with { Languages = LanguagePair.Default.Swap().Value, Theme = theme, UiLanguage = UiLanguage.English };

        var saved = await repository.SaveAsync(settings);
        var loaded = await repository.LoadAsync();

        Assert.True(saved.IsSuccess);
        Assert.Equal(settings, loaded);
    }

    [Fact]
    public async Task Ignores_the_google_section_of_older_settings_files()
    {
        Directory.CreateDirectory(_paths.Root);
        await File.WriteAllTextAsync(_paths.SettingsFile, """
            {
              "Version": 1,
              "Hotkey": "Ctrl+Alt+T",
              "Google": { "Mode": "ApiKey", "ApiKey": "dpapi:abc" }
            }
            """);

        var loaded = await new JsonSettingsRepository(_paths).LoadAsync();

        Assert.Equal(AppSettings.Default with { Hotkey = HotkeyGesture.Parse("Ctrl+Alt+T").Value }, loaded);
    }

    [Fact]
    public async Task Corrupt_file_yields_defaults()
    {
        Directory.CreateDirectory(_paths.Root);
        await File.WriteAllTextAsync(_paths.SettingsFile, "{ not json");

        var loaded = await new JsonSettingsRepository(_paths).LoadAsync();

        Assert.Equal(AppSettings.Default, loaded);
    }

    public void Dispose()
    {
        if (Directory.Exists(_paths.Root))
        {
            Directory.Delete(_paths.Root, recursive: true);
        }
    }
}
