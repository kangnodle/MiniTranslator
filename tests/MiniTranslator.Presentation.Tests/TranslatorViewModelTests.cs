using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Testing;

namespace MiniTranslator.Presentation.Tests;

public class TranslatorViewModelTests
{
    private sealed record Harness(
        TranslatorViewModel ViewModel,
        FakeTranslator Translator,
        FakeClipboard Clipboard,
        InMemorySettingsRepository Repository,
        SettingsStore Store);

    private static Harness Create()
    {
        var repository = new InMemorySettingsRepository();
        var store = new SettingsStore(repository);
        var translator = new FakeTranslator();
        var clipboard = new FakeClipboard();
        var viewModel = new TranslatorViewModel(
            new TranslateTextUseCase(translator), new ChangeLanguagesUseCase(store), store, clipboard);
        return new Harness(viewModel, translator, clipboard, repository, store);
    }

    [Fact]
    public void Prepare_with_text_translates_immediately()
    {
        var h = Create();

        h.ViewModel.Prepare("hello");

        Assert.Equal("hello", h.ViewModel.SourceText);
        Assert.Equal("[hello]", h.ViewModel.TranslatedText);
        Assert.Single(h.Translator.Calls);
    }

    [Fact]
    public void Prepare_with_auto_translate_false_populates_text_without_translating()
    {
        var h = Create();

        h.ViewModel.Prepare("hello", autoTranslate: false);

        Assert.Equal("hello", h.ViewModel.SourceText);
        Assert.Empty(h.ViewModel.TranslatedText);
        Assert.Empty(h.Translator.Calls);
    }

    [Fact]
    public void Prepare_without_text_keeps_the_previous_input()
    {
        var h = Create();
        h.ViewModel.SourceText = "draft";

        h.ViewModel.Prepare("   ");

        Assert.Equal("draft", h.ViewModel.SourceText);
        Assert.Empty(h.Translator.Calls);
    }

    [Fact]
    public void Changing_a_language_is_persisted_and_retranslates()
    {
        var h = Create();
        h.ViewModel.Prepare("hello");

        h.ViewModel.SelectedTarget = LanguageCatalog.FromCode("ja").Value;

        Assert.Equal("ja", h.Repository.Saved?.Languages.Target.Code);
        Assert.Equal(2, h.Translator.Calls.Count);
        Assert.Equal("ja", h.Translator.Calls[^1].Languages.Target.Code);
    }

    [Fact]
    public void Auto_detect_shows_the_detected_language_and_swap_uses_it()
    {
        var h = Create();
        h.Translator.DetectedLanguageCode = "fr";
        h.ViewModel.SelectedSource = LanguageCatalog.AutoDetect;

        h.ViewModel.Prepare("Bonjour");
        Assert.Equal("감지된 언어: Français (French)", h.ViewModel.InfoMessage);

        h.ViewModel.SwapLanguagesCommand.Execute(null);

        Assert.Equal("ko", h.ViewModel.SelectedSource.Code);
        Assert.Equal("fr", h.ViewModel.SelectedTarget.Code);
        Assert.Equal("[Bonjour]", h.ViewModel.SourceText);
    }

    [Fact]
    public void Copy_puts_the_translation_on_the_clipboard()
    {
        var h = Create();
        h.ViewModel.Prepare("hello");

        h.ViewModel.CopyCommand.Execute(null);

        Assert.Equal("[hello]", h.Clipboard.Text);
        Assert.Null(h.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task Follows_settings_saved_elsewhere()
    {
        var h = Create();
        var behavior = new BehaviorSettings(CaptureSelectionOnHotkey: true, HideOnDeactivate: false);

        await h.Store.UpdateAsync(s => s with { Languages = LanguagePair.Default.Swap().Value, Behavior = behavior });

        Assert.Equal("ko", h.ViewModel.SelectedSource.Code);
        Assert.Equal("en", h.ViewModel.SelectedTarget.Code);
        Assert.False(h.ViewModel.HideOnDeactivate);
    }
}
