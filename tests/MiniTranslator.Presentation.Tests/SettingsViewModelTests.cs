using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Testing;

namespace MiniTranslator.Presentation.Tests;

public class SettingsViewModelTests
{
    private const int T = 'T';

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.TomorrowNightBlue)]
    [InlineData(AppTheme.SolarizedLight)]
    [InlineData(AppTheme.SolarizedDark)]
    public async Task Theme_is_applied_before_persistence_finishes(AppTheme theme)
    {
        var repository = new DelayedSettingsRepository();
        var store = new SettingsStore(repository);
        using var manager = new HotkeyManager(new FakeGlobalHotkey());
        var vm = new SettingsViewModel(new SettingsEditor(store, manager, new FakeStartupRegistration(), new FakeCodexAccount()));
        AppTheme? applied = null;
        vm.ThemeChanged += (_, theme) => applied = theme;

        var pending = vm.SetThemeCommand.ExecuteAsync(theme);

        Assert.False(pending.IsCompleted);
        Assert.Equal(theme, vm.Theme);
        Assert.Equal(theme, applied);
        Assert.Equal(AppTheme.System, store.Current.Theme);
        repository.Completion.SetResult(Result.Success());
        await pending;
        Assert.Equal(theme, store.Current.Theme);
    }

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.TomorrowNightBlue)]
    [InlineData(AppTheme.SolarizedLight)]
    [InlineData(AppTheme.SolarizedDark)]
    public async Task Settings_reopened_with_a_saved_theme_selects_that_theme(AppTheme theme)
    {
        var store = new SettingsStore(new InMemorySettingsRepository(AppSettings.Default with { Theme = theme }));
        await store.InitializeAsync();
        using var manager = new HotkeyManager(new FakeGlobalHotkey());
        var vm = new SettingsViewModel(new SettingsEditor(store, manager, new FakeStartupRegistration(), new FakeCodexAccount()));

        Assert.Equal(theme, vm.Theme);
    }

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.TomorrowNightBlue)]
    [InlineData(AppTheme.SolarizedLight)]
    [InlineData(AppTheme.SolarizedDark)]
    public async Task Theme_is_saved_immediately_without_committing_the_other_draft_fields(AppTheme theme)
    {
        var (vm, repository, _) = Create();
        var applied = new List<AppTheme>();
        vm.ThemeChanged += (_, theme) => applied.Add(theme);
        vm.HideOnDeactivate = false;
        vm.SwapLanguagesCommand.Execute(null);

        await vm.SetThemeCommand.ExecuteAsync(theme);
        vm.CancelCommand.Execute(null);

        Assert.Equal(theme, vm.Theme);
        Assert.Equal(AppSettings.Default with { Theme = theme }, repository.Saved);
        Assert.Equal([theme], applied);
        Assert.Null(vm.ThemeError);
    }

    [Fact]
    public async Task Saving_the_other_settings_keeps_the_immediate_theme()
    {
        var (vm, repository, _) = Create();
        await vm.SetThemeCommand.ExecuteAsync(AppTheme.Dark);
        vm.HideOnDeactivate = false;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(AppTheme.Dark, repository.Saved?.Theme);
        Assert.False(repository.Saved?.Behavior.HideOnDeactivate);
    }

    [Theory]
    [InlineData(AppTheme.Dark)]
    [InlineData(AppTheme.TomorrowNightBlue)]
    [InlineData(AppTheme.SolarizedLight)]
    [InlineData(AppTheme.SolarizedDark)]
    public async Task Failed_theme_save_restores_the_previous_theme_and_reports_the_error(AppTheme theme)
    {
        var repository = new InMemorySettingsRepository { FailSaveWith = new Error("Disk", "디스크가 가득 찼습니다.") };
        var (vm, _, _) = Create(repository);
        var applied = new List<AppTheme>();
        vm.ThemeChanged += (_, theme) => applied.Add(theme);

        await vm.SetThemeCommand.ExecuteAsync(theme);

        Assert.Equal(AppTheme.System, vm.Theme);
        Assert.Equal([theme, AppTheme.System], applied);
        Assert.Null(repository.Saved);
        Assert.Equal("디스크가 가득 찼습니다.", vm.ThemeError);
    }

    [Fact]
    public async Task Invalid_theme_does_not_change_or_save_settings()
    {
        var (vm, repository, _) = Create();

        await vm.SetThemeCommand.ExecuteAsync((AppTheme)99);

        Assert.Equal(AppTheme.System, vm.Theme);
        Assert.Equal(0, repository.SaveCount);
    }

    private static (SettingsViewModel ViewModel, InMemorySettingsRepository Repository, FakeGlobalHotkey Hotkey) Create(
        InMemorySettingsRepository? repository = null)
    {
        repository ??= new InMemorySettingsRepository();
        var store = new SettingsStore(repository);
        var hotkey = new FakeGlobalHotkey();
        var manager = new HotkeyManager(hotkey);
        manager.Apply(store.Current.Hotkey);
        return (new SettingsViewModel(new SettingsEditor(store, manager, new FakeStartupRegistration(), new FakeCodexAccount())), repository, hotkey);
    }

    private sealed class DelayedSettingsRepository : ISettingsRepository
    {
        public TaskCompletionSource<Result<Unit>> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(AppSettings.Default);

        public Task<Result<Unit>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => Completion.Task;
    }

    [Fact]
    public void Recording_an_invalid_chord_keeps_the_hotkey_and_explains_why()
    {
        var (vm, _, _) = Create();

        vm.RecordHotkeyCommand.Execute(new KeyChord(HotkeyModifiers.Shift, T));

        Assert.Equal(HotkeyGesture.Default, vm.Hotkey);
        Assert.Equal(HotkeyErrors.ShiftOnly.Message, vm.HotkeyError);
    }

    [Fact]
    public async Task Save_applies_the_draft_and_requests_close()
    {
        var (vm, repository, hotkey) = Create();
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.RecordHotkeyCommand.Execute(new KeyChord(HotkeyModifiers.Control | HotkeyModifiers.Alt, T));
        vm.HideOnDeactivate = false;
        vm.Font = AppFont.Gulim;
        await vm.SaveCommand.ExecuteAsync(null);

        var expected = HotkeyGesture.Create(HotkeyModifiers.Control | HotkeyModifiers.Alt, T).Value;
        Assert.True(closed);
        Assert.Equal(AppFont.Gulim, repository.Saved?.Font);
        Assert.Equal(expected, hotkey.Registered);
        Assert.Equal(expected, repository.Saved?.Hotkey);
        Assert.False(repository.Saved?.Behavior.HideOnDeactivate);
    }

    [Fact]
    public async Task Save_applies_the_chosen_translation_engine()
    {
        var (vm, repository, _) = Create();
        Assert.Equal(TranslationEngine.Claude, vm.Engine);

        vm.Engine = TranslationEngine.Codex;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(TranslationEngine.Codex, repository.Saved?.Engine);
    }

    [Fact]
    public async Task Save_applies_the_model_and_effort_of_each_engine()
    {
        var (vm, repository, _) = Create();
        Assert.Equal("", vm.ClaudeModel);

        vm.ClaudeModel = " opus ";
        vm.ClaudeEffort = "low";
        vm.CodexEffort = "high";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(new EngineOptions("opus", "low"), repository.Saved?.Claude);
        Assert.Equal(new EngineOptions("", "high"), repository.Saved?.Codex);
    }

    [Fact]
    public async Task Save_applies_the_codex_engine_and_its_executable_path()
    {
        var (vm, repository, _) = Create();
        Assert.Equal("", vm.CodexExecutablePath);

        vm.Engine = TranslationEngine.Codex;
        vm.CodexExecutablePath = @"""C:\Tools\codex.exe""";
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(TranslationEngine.Codex, repository.Saved?.Engine);
        Assert.Equal(@"C:\Tools\codex.exe", repository.Saved?.CodexExecutablePath);
    }

    [Fact]
    public async Task Failed_save_shows_the_error_and_stays_open()
    {
        var (vm, _, _) = Create(new InMemorySettingsRepository { FailSaveWith = new Error("Disk", "디스크가 가득 찼습니다.") });
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.CaptureSelectionOnHotkey = false;
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(closed);
        Assert.Equal("디스크가 가득 찼습니다.", vm.SaveError);
    }

    [Fact]
    public void Swap_in_the_draft_does_not_touch_saved_settings()
    {
        var (vm, repository, _) = Create();

        vm.SwapLanguagesCommand.Execute(null);

        Assert.Equal("ko", vm.SourceLanguage.Code);
        Assert.Equal("en", vm.TargetLanguage.Code);
        Assert.Null(repository.Saved);
    }

    [Fact]
    public void Failed_hotkey_resume_shows_the_error_and_prevents_closing()
    {
        var (vm, _, hotkey) = Create();
        vm.BeginHotkeyRecording();
        hotkey.Taken.Add(HotkeyGesture.Default);

        Assert.False(vm.EndHotkeyRecording());
        Assert.Null(hotkey.Registered);
        Assert.Equal(HotkeyErrors.AlreadyInUse(HotkeyGesture.Default).Message, vm.HotkeyError);
    }

    [Fact]
    public async Task A_different_hotkey_can_be_saved_after_resume_fails()
    {
        var (vm, _, hotkey) = Create();
        vm.BeginHotkeyRecording();
        hotkey.Taken.Add(HotkeyGesture.Default);
        Assert.False(vm.EndHotkeyRecording());
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.RecordHotkeyCommand.Execute(new KeyChord(HotkeyModifiers.Control | HotkeyModifiers.Alt, T));
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(closed);
        Assert.True(vm.EndHotkeyRecording());
        Assert.Equal(vm.Hotkey, hotkey.Registered);
        Assert.Null(vm.HotkeyError);
    }
}
