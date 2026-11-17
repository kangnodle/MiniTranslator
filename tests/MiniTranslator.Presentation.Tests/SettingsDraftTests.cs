using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Testing;

namespace MiniTranslator.Presentation.Tests;

public class SettingsDraftTests
{
    [Fact]
    public async Task Saving_behavior_preserves_the_language_changed_after_the_window_opened()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var manager = new HotkeyManager(new FakeGlobalHotkey());
        var viewModel = new SettingsViewModel(new SettingsEditor(store, manager, new FakeStartupRegistration(), new FakeCodexAccount()));
        var japanese = LanguagePair.Create("en", "ja").Value;
        await new ChangeLanguagesUseCase(store).ExecuteAsync(japanese);

        viewModel.HideOnDeactivate = false;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(japanese, store.Current.Languages);
        Assert.False(store.Current.Behavior.HideOnDeactivate);
    }

    [Fact]
    public async Task Each_behavior_field_preserves_changes_made_elsewhere()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var manager = new HotkeyManager(new FakeGlobalHotkey());
        var viewModel = new SettingsViewModel(new SettingsEditor(store, manager, new FakeStartupRegistration(), new FakeCodexAccount()));
        await store.UpdateAsync(s => s with { Behavior = s.Behavior with { CaptureSelectionOnHotkey = false } });

        viewModel.HideOnDeactivate = false;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.False(store.Current.Behavior.CaptureSelectionOnHotkey);
        Assert.False(store.Current.Behavior.HideOnDeactivate);
    }

    [Fact]
    public async Task An_edited_language_wins_without_restoring_an_untouched_old_hotkey()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var hotkey = new FakeGlobalHotkey();
        var manager = new HotkeyManager(hotkey);
        var viewModel = new SettingsViewModel(new SettingsEditor(store, manager, new FakeStartupRegistration(), new FakeCodexAccount()));
        var newHotkey = HotkeyGesture.Parse("Ctrl+Alt+T").Value;
        manager.Apply(newHotkey);
        await store.UpdateAsync(s => s with { Hotkey = newHotkey, Languages = LanguagePair.Create("en", "fr").Value });

        viewModel.TargetLanguage = LanguageCatalog.FromCode("ja").Value;
        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal("ja", store.Current.Languages.Target.Code);
        Assert.Equal(newHotkey, store.Current.Hotkey);
        Assert.Equal(newHotkey, hotkey.Registered);
    }
}
