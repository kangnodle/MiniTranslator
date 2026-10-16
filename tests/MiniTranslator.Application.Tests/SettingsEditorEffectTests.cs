using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Testing;

namespace MiniTranslator.Application.Tests;

public class SettingsEditorEffectTests
{
    private static readonly HotkeyGesture CtrlAltT = HotkeyGesture.Parse("Ctrl+Alt+T").Value;

    [Fact]
    public async Task Validation_failure_never_changes_startup_hotkeys_or_storage()
    {
        var repository = new InMemorySettingsRepository();
        var hotkey = new FakeGlobalHotkey();
        var startup = new FakeStartupRegistration();
        var editor = new SettingsEditor(new SettingsStore(repository), new HotkeyManager(hotkey), startup);

        var saved = await editor.SaveAsync(editor.Original with { UiLanguage = (UiLanguage)99 }, startWithWindows: true);
        var theme = await editor.SetThemeAsync((AppTheme)99);

        Assert.Equal(SettingsErrors.InvalidUiLanguage, saved.Settings.Error);
        Assert.Equal(SettingsErrors.InvalidTheme, theme.Error);
        Assert.Empty(startup.Changes);
        Assert.Null(hotkey.Registered);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task A_hotkey_conflict_restores_startup_without_saving()
    {
        var repository = new InMemorySettingsRepository();
        var hotkey = new FakeGlobalHotkey();
        hotkey.Taken.Add(CtrlAltT);
        var startup = new FakeStartupRegistration();
        var editor = new SettingsEditor(new SettingsStore(repository), new HotkeyManager(hotkey), startup);

        var result = await editor.SaveAsync(editor.Original with { Hotkey = CtrlAltT }, startWithWindows: true);

        Assert.Equal("Hotkey.AlreadyInUse", result.Settings.Error.Code);
        Assert.Equal(new[] { true, false }, startup.Changes);
        Assert.False(startup.Enabled);
        Assert.Null(result.StartupRollbackError);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task A_failed_startup_rollback_is_returned_separately_and_retry_can_correct_it()
    {
        var failure = new Error("Settings.SaveFailed", "disk full");
        var repository = new InMemorySettingsRepository { FailSaveWith = failure };
        var startup = new FakeStartupRegistration { FailWhenEnabled = false };
        var editor = new SettingsEditor(new SettingsStore(repository), new HotkeyManager(new FakeGlobalHotkey()), startup);
        var draft = editor.Original with { Behavior = new(false, false) };

        var result = await editor.SaveAsync(draft, startWithWindows: true);

        Assert.Equal(failure, result.Settings.Error);
        Assert.Equal("Startup.WriteFailed", result.StartupRollbackError!.Code);
        Assert.True(startup.Enabled);
        repository.FailSaveWith = null;
        startup.FailWhenEnabled = null;

        var retry = await editor.SaveAsync(draft, startWithWindows: false);

        Assert.True(retry.Settings.IsSuccess);
        Assert.False(startup.Enabled);
        Assert.Null(retry.StartupRollbackError);
    }

    [Fact]
    public async Task A_failed_hotkey_rollback_is_reported_with_the_original_save_failure()
    {
        var failure = new Error("Settings.SaveFailed", "disk full");
        var repository = new InMemorySettingsRepository { FailSaveWith = failure };
        var hotkey = new FakeGlobalHotkey();
        var manager = new HotkeyManager(hotkey);
        manager.Apply(HotkeyGesture.Default);
        var editor = new SettingsEditor(new SettingsStore(repository), manager, new FakeStartupRegistration());
        hotkey.Taken.Add(HotkeyGesture.Default);

        var result = await editor.SaveAsync(editor.Original with { Hotkey = CtrlAltT }, startWithWindows: false);

        Assert.Equal(failure, result.Settings.Error);
        Assert.Equal("Hotkey.AlreadyInUse", result.HotkeyRollbackError!.Code);
        Assert.Equal(AppSettings.Default, editor.Current);
    }

    [Fact]
    public async Task Cancelling_persistence_restores_both_desktop_effects_and_does_not_publish_the_draft()
    {
        var repository = new BlockingSettingsRepository();
        var store = new SettingsStore(repository);
        var hotkey = new FakeGlobalHotkey();
        var manager = new HotkeyManager(hotkey);
        manager.Apply(HotkeyGesture.Default);
        var startup = new FakeStartupRegistration();
        var editor = new SettingsEditor(store, manager, startup);
        using var cancellation = new CancellationTokenSource();
        var published = false;
        store.Changed += (_, _) => published = true;

        var save = editor.SaveAsync(editor.Original with { Hotkey = CtrlAltT }, startWithWindows: true, cancellation.Token);
        await repository.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(CtrlAltT, hotkey.Registered);
        Assert.True(startup.Enabled);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => save);

        Assert.Equal(HotkeyGesture.Default, hotkey.Registered);
        Assert.False(startup.Enabled);
        Assert.Equal(AppSettings.Default, store.Current);
        Assert.False(published);
    }

    private sealed class BlockingSettingsRepository : ISettingsRepository
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(AppSettings.Default);

        public async Task<Result<Unit>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return Result.Success();
        }
    }
}
