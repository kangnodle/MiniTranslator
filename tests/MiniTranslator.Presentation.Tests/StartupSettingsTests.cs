using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Common;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Testing;

namespace MiniTranslator.Presentation.Tests;

public class StartupSettingsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Startup_is_loaded_and_changes_only_on_save(bool enabled)
    {
        var startup = new FakeStartupRegistration { Enabled = enabled };
        var vm = Create(startup, new InMemorySettingsRepository());
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;
        Assert.Equal(enabled, vm.StartWithWindows);

        vm.StartWithWindows = !enabled;
        Assert.Equal(enabled, startup.Enabled);
        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(!enabled, startup.Enabled);
        Assert.Equal(new[] { !enabled }, startup.Changes);
        Assert.True(closed);
        Assert.Equal(!enabled, Create(startup, new InMemorySettingsRepository()).StartWithWindows);
    }

    [Fact]
    public void Cancel_does_not_register_startup()
    {
        var startup = new FakeStartupRegistration();
        var vm = Create(startup, new InMemorySettingsRepository());
        vm.StartWithWindows = true;

        vm.CancelCommand.Execute(null);

        Assert.False(startup.Enabled);
        Assert.Empty(startup.Changes);
    }

    [Fact]
    public async Task An_untouched_draft_preserves_external_startup_changes()
    {
        var startup = new FakeStartupRegistration();
        var vm = Create(startup, new InMemorySettingsRepository());
        startup.Enabled = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(startup.Enabled);
        Assert.Empty(startup.Changes);
    }

    [Fact]
    public async Task Registration_failure_prevents_saving_and_keeps_the_window_open()
    {
        var startup = new FakeStartupRegistration { FailWhenEnabled = true };
        var repository = new InMemorySettingsRepository();
        var vm = Create(startup, repository);
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;
        vm.StartWithWindows = true;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(startup.Enabled);
        Assert.Equal(0, repository.SaveCount);
        Assert.NotNull(vm.SaveError);
        Assert.False(closed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Persistence_failure_restores_startup(bool enabled)
    {
        var startup = new FakeStartupRegistration { Enabled = enabled };
        var repository = new InMemorySettingsRepository { FailSaveWith = new Error("Settings.SaveFailed", "저장 실패") };
        var vm = Create(startup, repository);
        vm.StartWithWindows = !enabled;
        vm.HideOnDeactivate = false;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(enabled, startup.Enabled);
        Assert.Equal(new[] { !enabled, enabled }, startup.Changes);
        Assert.Equal("저장 실패", vm.SaveError);
    }

    [Fact]
    public async Task Failed_rollback_is_reported_and_can_be_corrected_on_retry()
    {
        var startup = new FakeStartupRegistration { FailWhenEnabled = false };
        var repository = new InMemorySettingsRepository { FailSaveWith = new Error("Settings.SaveFailed", "저장 실패") };
        var vm = Create(startup, repository);
        vm.StartWithWindows = true;
        vm.HideOnDeactivate = false;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(startup.Enabled);
        Assert.Contains("되돌리지 못했습니다", vm.SaveError);
        startup.FailWhenEnabled = null;
        repository.FailSaveWith = null;
        vm.StartWithWindows = false;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(startup.Enabled);
        Assert.Null(vm.SaveError);
    }

    [Fact]
    public async Task Read_failure_disables_only_startup_and_allows_other_settings_to_save()
    {
        var startup = new FakeStartupRegistration { ReadError = new Error("Startup.ReadFailed", "읽기 실패") };
        var repository = new InMemorySettingsRepository();
        var vm = Create(startup, repository);
        Assert.False(vm.CanConfigureStartup);
        Assert.Equal("읽기 실패", vm.StartupError);
        vm.HideOnDeactivate = false;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(repository.Saved!.Behavior.HideOnDeactivate);
        Assert.Empty(startup.Changes);
    }

    private static SettingsViewModel Create(FakeStartupRegistration startup, InMemorySettingsRepository repository)
    {
        var store = new SettingsStore(repository);
        var manager = new HotkeyManager(new FakeGlobalHotkey());
        return new SettingsViewModel(new SettingsEditor(store, manager, startup, new FakeCodexAccount()));
    }
}
