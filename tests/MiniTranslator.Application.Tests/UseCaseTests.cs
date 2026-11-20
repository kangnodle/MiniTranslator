using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Testing;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.Translations;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Application.Tests;

public class SettingsStoreTests
{
    [Fact]
    public async Task Update_persists_then_publishes()
    {
        var repository = new InMemorySettingsRepository();
        var store = new SettingsStore(repository);
        AppSettings? published = null;
        store.Changed += (_, s) => published = s;

        var swapped = LanguagePair.Default.Swap().Value;
        var result = await store.UpdateAsync(s => s with { Languages = swapped });

        Assert.True(result.IsSuccess);
        Assert.Equal(swapped, store.Current.Languages);
        Assert.Equal(store.Current, repository.Saved);
        Assert.Equal(store.Current, published);
    }

    [Fact]
    public async Task Update_without_changes_does_not_save()
    {
        var repository = new InMemorySettingsRepository();
        var store = new SettingsStore(repository);

        await store.UpdateAsync(s => s);

        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Failed_save_keeps_the_current_settings()
    {
        var repository = new InMemorySettingsRepository { FailSaveWith = new Error("Disk", "full") };
        var store = new SettingsStore(repository);

        var result = await store.UpdateAsync(s => s with { Languages = LanguagePair.Default.Swap().Value });

        Assert.Equal("Disk", result.Error.Code);
        Assert.Equal(AppSettings.Default, store.Current);
    }
}

public class TranslateTextUseCaseTests
{
    private static (TranslateTextUseCase UseCase, FakeTranslator Translator) Create()
    {
        var translator = new FakeTranslator();
        return (new TranslateTextUseCase(translator), translator);
    }

    [Fact]
    public async Task Translates_without_any_credentials()
    {
        var (useCase, translator) = Create();

        var result = await useCase.ExecuteAsync("  hello ", LanguagePair.Default);

        Assert.Equal("[hello]", result.Value.TranslatedText);
        var request = Assert.Single(translator.Calls);
        Assert.Equal("hello", request.Text);
    }

    [Fact]
    public async Task Does_not_call_the_provider_for_blank_text()
    {
        var (useCase, translator) = Create();

        var result = await useCase.ExecuteAsync("   ", LanguagePair.Default);

        Assert.Equal(TranslationErrors.EmptyText, result.Error);
        Assert.Empty(translator.Calls);
    }
}

public class SelectedEngineTranslatorTests
{
    private static readonly TranslationRequest Request = TranslationRequest.Create("hello", LanguagePair.Default).Value;

    [Fact]
    public async Task Uses_the_engine_selected_when_each_request_is_made()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var claude = new FakeTranslator();
        var codex = new FakeTranslator();
        var translator = new SelectedEngineTranslator(store, engine => engine == TranslationEngine.Codex ? codex : claude);

        await translator.TranslateAsync(Request);
        await store.UpdateAsync(s => s with { Engine = TranslationEngine.Codex });
        await translator.TranslateAsync(Request);
        await translator.TranslateAsync(Request);

        Assert.Single(claude.Calls);
        Assert.Equal(2, codex.Calls.Count);
    }

    [Fact]
    public async Task Does_not_resolve_an_engine_that_is_never_selected()
    {
        var resolved = new List<TranslationEngine>();
        var translator = new SelectedEngineTranslator(new SettingsStore(new InMemorySettingsRepository()), engine =>
        {
            resolved.Add(engine);
            return new FakeTranslator();
        });

        await translator.TranslateAsync(Request);

        Assert.Equal([TranslationEngine.Claude], resolved);
    }
}

public class ChangeLanguagesUseCaseTests
{
    [Fact]
    public async Task Persists_only_the_language_pair()
    {
        var repository = new InMemorySettingsRepository();
        var store = new SettingsStore(repository);
        var swapped = LanguagePair.Default.Swap().Value;

        var result = await new ChangeLanguagesUseCase(store).ExecuteAsync(swapped);

        Assert.True(result.IsSuccess);
        Assert.Equal(AppSettings.Default with { Languages = swapped }, repository.Saved);
    }
}

public class HotkeyManagerTests
{
    private static readonly HotkeyGesture CtrlAltT = HotkeyGesture.Parse("Ctrl+Alt+T").Value;

    [Fact]
    public void Apply_registers_the_gesture()
    {
        var hotkey = new FakeGlobalHotkey();
        var manager = new HotkeyManager(hotkey);

        Assert.True(manager.Apply(HotkeyGesture.Default).IsSuccess);
        Assert.Equal(HotkeyGesture.Default, hotkey.Registered);
        Assert.Equal(HotkeyGesture.Default, manager.Active);
    }

    [Fact]
    public void Failed_change_restores_the_previous_gesture()
    {
        var hotkey = new FakeGlobalHotkey();
        var manager = new HotkeyManager(hotkey);
        manager.Apply(HotkeyGesture.Default);
        hotkey.Taken.Add(CtrlAltT);

        var result = manager.Apply(CtrlAltT);

        Assert.Equal("Hotkey.AlreadyInUse", result.Error.Code);
        Assert.Equal(HotkeyGesture.Default, hotkey.Registered);
        Assert.Equal(HotkeyGesture.Default, manager.Active);
    }

    [Fact]
    public void Suspend_and_resume_release_and_restore_the_gesture()
    {
        var hotkey = new FakeGlobalHotkey();
        var manager = new HotkeyManager(hotkey);
        manager.Apply(HotkeyGesture.Default);

        manager.Suspend();
        Assert.Null(hotkey.Registered);

        manager.Resume();
        Assert.Equal(HotkeyGesture.Default, hotkey.Registered);
    }

    [Fact]
    public void Pressed_is_forwarded()
    {
        var hotkey = new FakeGlobalHotkey();
        var manager = new HotkeyManager(hotkey);
        var pressed = 0;
        manager.Pressed += (_, _) => pressed++;

        hotkey.Press();

        Assert.Equal(1, pressed);
    }
}

public class SettingsEditorTests
{
    private static readonly HotkeyGesture CtrlAltT = HotkeyGesture.Parse("Ctrl+Alt+T").Value;

    [Fact]
    public async Task Registers_the_new_hotkey_and_persists_the_settings()
    {
        var repository = new InMemorySettingsRepository();
        var hotkey = new FakeGlobalHotkey();
        var editor = new SettingsEditor(new SettingsStore(repository), new HotkeyManager(hotkey), new FakeStartupRegistration(), new FakeCodexAccount());
        var draft = AppSettings.Default with { Hotkey = CtrlAltT };

        var result = await editor.SaveAsync(draft, startWithWindows: false);

        Assert.True(result.Settings.IsSuccess);
        Assert.Equal(CtrlAltT, hotkey.Registered);
        Assert.Equal(draft, repository.Saved);
    }

    [Fact]
    public async Task Does_not_save_when_the_hotkey_is_taken()
    {
        var repository = new InMemorySettingsRepository();
        var hotkey = new FakeGlobalHotkey();
        var manager = new HotkeyManager(hotkey);
        manager.Apply(HotkeyGesture.Default);
        hotkey.Taken.Add(CtrlAltT);
        var editor = new SettingsEditor(new SettingsStore(repository), manager, new FakeStartupRegistration(), new FakeCodexAccount());

        var result = await editor.SaveAsync(AppSettings.Default with { Hotkey = CtrlAltT }, startWithWindows: false);

        Assert.Equal("Hotkey.AlreadyInUse", result.Settings.Error.Code);
        Assert.Equal(0, repository.SaveCount);
        Assert.Equal(HotkeyGesture.Default, hotkey.Registered);
    }

    [Fact]
    public async Task Restores_the_previous_hotkey_when_saving_fails()
    {
        var repository = new InMemorySettingsRepository { FailSaveWith = new Error("Disk", "full") };
        var hotkey = new FakeGlobalHotkey();
        var manager = new HotkeyManager(hotkey);
        manager.Apply(HotkeyGesture.Default);
        var editor = new SettingsEditor(new SettingsStore(repository), manager, new FakeStartupRegistration(), new FakeCodexAccount());

        var result = await editor.SaveAsync(AppSettings.Default with { Hotkey = CtrlAltT }, startWithWindows: false);

        Assert.True(result.Settings.IsFailure);
        Assert.Equal(HotkeyGesture.Default, hotkey.Registered);
    }
}

public class CaptureSelectionUseCaseTests
{
    [Fact]
    public async Task Returns_trimmed_selection_when_enabled()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var useCase = new CaptureSelectionUseCase(new FakeSelectionReader("  selected \r\n"), store);

        Assert.Equal("selected", await useCase.ExecuteAsync());
    }

    [Fact]
    public async Task Does_not_touch_the_selection_when_disabled()
    {
        var settings = AppSettings.Default with { Behavior = new BehaviorSettings(CaptureSelectionOnHotkey: false, HideOnDeactivate: true) };
        var store = new SettingsStore(new InMemorySettingsRepository(settings));
        await store.InitializeAsync();
        var reader = new FakeSelectionReader("selected");

        Assert.Null(await new CaptureSelectionUseCase(reader, store).ExecuteAsync());
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task Blank_selection_is_treated_as_none()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        Assert.Null(await new CaptureSelectionUseCase(new FakeSelectionReader("  "), store).ExecuteAsync());
    }
}
