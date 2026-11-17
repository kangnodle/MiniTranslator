using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;
using MiniTranslator.Presentation.Localization;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Testing;

namespace MiniTranslator.Presentation.Tests;

public class UiLanguageTests
{
    [Theory]
    [InlineData(UiLanguage.Korean, UiLanguage.English)]
    [InlineData(UiLanguage.English, UiLanguage.Korean)]
    public async Task Language_is_a_draft_until_saved_and_is_restored_when_reopened(UiLanguage original, UiLanguage next)
    {
        var repository = new InMemorySettingsRepository(AppSettings.Default with { UiLanguage = original });
        var store = new SettingsStore(repository);
        await store.InitializeAsync();
        var vm = Create(store);
        Assert.Equal(original, vm.UiLanguage);
        vm.UiLanguage = next;
        Assert.Equal(original, store.Current.UiLanguage);

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(next, repository.Saved!.UiLanguage);
        var reloaded = new SettingsStore(repository);
        await reloaded.InitializeAsync();
        Assert.Equal(next, Create(reloaded).UiLanguage);
        Assert.Equal(LanguagePair.Default, store.Current.Languages);
    }

    [Fact]
    public void Cancel_does_not_change_the_ui_language()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var vm = Create(store);
        vm.UiLanguage = UiLanguage.English;

        vm.CancelCommand.Execute(null);

        Assert.Equal(UiLanguage.Korean, store.Current.UiLanguage);
    }

    [Fact]
    public async Task Failed_save_preserves_the_language_and_reports_an_english_error()
    {
        var repository = new InMemorySettingsRepository(AppSettings.Default with { UiLanguage = UiLanguage.English })
        {
            FailSaveWith = new Error("Settings.SaveFailed", "설정을 저장하지 못했습니다.\ndisk full"),
        };
        var store = new SettingsStore(repository);
        await store.InitializeAsync();
        var vm = Create(store);
        vm.UiLanguage = UiLanguage.Korean;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(UiLanguage.English, store.Current.UiLanguage);
        Assert.Equal("Could not save the settings.\ndisk full", vm.SaveError);
    }

    [Fact]
    public async Task An_untouched_language_draft_preserves_a_change_saved_elsewhere()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var vm = Create(store);
        await store.UpdateAsync(current => current with { UiLanguage = UiLanguage.English });
        vm.HideOnDeactivate = false;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(UiLanguage.English, store.Current.UiLanguage);
    }

    [Fact]
    public async Task Invalid_ui_language_cannot_be_saved()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var vm = Create(store);
        vm.UiLanguage = (UiLanguage)99;

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(UiLanguage.Korean, store.Current.UiLanguage);
        Assert.NotNull(vm.SaveError);
    }

    [Fact]
    public async Task Existing_status_messages_follow_language_changes_without_retranslating_text()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var translator = new FakeTranslator { DetectedLanguageCode = "ko" };
        var vm = new TranslatorViewModel(new TranslateTextUseCase(translator), new ChangeLanguagesUseCase(store), store, new FakeClipboard());
        vm.SelectedSource = LanguageCatalog.AutoDetect;
        vm.Prepare("hello");
        Assert.Equal("감지된 언어: 한국어 (Korean)", vm.InfoMessage);
        var calls = translator.Calls.Count;
        var result = vm.TranslatedText;

        await store.UpdateAsync(current => current with { UiLanguage = UiLanguage.English });

        Assert.Equal("Detected language: Korean", vm.InfoMessage);
        Assert.Equal(result, vm.TranslatedText);
        Assert.Equal(calls, translator.Calls.Count);
        vm.CopyCommand.Execute(null);
        Assert.Equal("Translation copied to the clipboard.", vm.InfoMessage);
        await store.UpdateAsync(current => current with { UiLanguage = UiLanguage.Korean });
        Assert.Equal("번역 결과를 클립보드에 복사했습니다.", vm.InfoMessage);

        vm.SourceText = new string('a', 5001);
        await vm.TranslateCommand.ExecuteAsync(null);
        Assert.StartsWith("한 번에 최대", vm.ErrorMessage);
        await store.UpdateAsync(current => current with { UiLanguage = UiLanguage.English });
        Assert.StartsWith("You can translate up to", vm.ErrorMessage);
        Assert.Contains("5,001", vm.ErrorMessage);
    }

    [Fact]
    public void Resource_keys_match_and_errors_keep_their_parameters_and_diagnostics()
    {
        Assert.Equal(UiText.All(UiLanguage.Korean).Select(entry => entry.Key).Order(),
            UiText.All(UiLanguage.English).Select(entry => entry.Key).Order());
        Assert.Equal("Settings", UiText.Get("Settings", UiLanguage.English));
        Assert.Equal("설정", UiText.Get("Settings", UiLanguage.Korean));
        Assert.Contains("Please try again later", UiText.Error(TranslationErrors.RateLimited, UiLanguage.English));
        Assert.Equal(TranslationErrors.RateLimited.Message, UiText.Error(TranslationErrors.RateLimited, UiLanguage.Korean));
        Assert.Contains("Ctrl + Space", UiText.Error(HotkeyErrors.AlreadyInUse(HotkeyGesture.Default), UiLanguage.English));
        Assert.Equal("The translation service rejected the request. (HTTP 429)\nquota exceeded",
            UiText.Error(TranslationErrors.ProviderRejected(429, "quota exceeded"), UiLanguage.English));
        Assert.Contains("5,001", UiText.Error(TranslationErrors.TooLong(5001), UiLanguage.English));
        Assert.Equal("technical detail", UiText.Error(new Error("Unknown", "technical detail"), UiLanguage.English));
    }

    private static SettingsViewModel Create(SettingsStore store)
    {
        var hotkeys = new HotkeyManager(new FakeGlobalHotkey());
        return new SettingsViewModel(new SettingsEditor(store, hotkeys, new FakeStartupRegistration(), new FakeCodexAccount()));
    }
}
