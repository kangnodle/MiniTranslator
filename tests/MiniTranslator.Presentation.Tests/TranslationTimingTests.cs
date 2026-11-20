using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Translations;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Testing;

namespace MiniTranslator.Presentation.Tests;

public class TranslationTimingTests
{
    [Fact]
    public async Task Settings_language_change_restarts_translation_and_ignores_the_old_response()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var translator = new DelayedTranslator();
        var viewModel = Create(store, translator);
        viewModel.SourceText = "hello";
        var oldRequest = viewModel.TranslateCommand.ExecuteAsync(null);

        await new ChangeLanguagesUseCase(store).ExecuteAsync(LanguagePair.Create("en", "ja").Value);

        Assert.Equal(2, translator.Requests.Count);
        Assert.True(translator.Requests[0].Token.IsCancellationRequested);
        Assert.Equal("ja", translator.Requests[1].Request.Languages.Target.Code);
        translator.Complete(1, "Japanese output");
        await viewModel.TranslateCommand.ExecutionTask!;
        translator.Complete(0, "Old Korean output");
        await oldRequest;

        Assert.Equal("Japanese output", viewModel.TranslatedText);
        Assert.Equal("ja", viewModel.SelectedTarget.Code);
        Assert.False(viewModel.IsTranslating);
    }

    [Fact]
    public async Task Language_change_clears_the_previous_result_while_the_new_request_is_pending()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var translator = new DelayedTranslator();
        var viewModel = Create(store, translator);
        viewModel.SourceText = "hello";
        var first = viewModel.TranslateCommand.ExecuteAsync(null);
        translator.Complete(0, "Korean output");
        await first;

        await new ChangeLanguagesUseCase(store).ExecuteAsync(LanguagePair.Create("en", "ja").Value);

        Assert.Empty(viewModel.TranslatedText);
        Assert.False(viewModel.CopyCommand.CanExecute(null));
        translator.Complete(1, "Japanese output");
        await viewModel.TranslateCommand.ExecutionTask!;
    }

    [Fact]
    public async Task Language_change_with_empty_input_cancels_without_starting_another_request()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var translator = new DelayedTranslator();
        var viewModel = Create(store, translator);
        viewModel.SourceText = "hello";
        var pending = viewModel.TranslateCommand.ExecuteAsync(null);
        viewModel.SourceText = string.Empty;

        await new ChangeLanguagesUseCase(store).ExecuteAsync(LanguagePair.Create("en", "ja").Value);
        translator.Complete(0, "Old Korean output");
        await pending;

        Assert.Single(translator.Requests);
        Assert.Empty(viewModel.TranslatedText);
        Assert.False(viewModel.IsTranslating);
    }

    [Fact]
    public async Task Streaming_updates_translated_text_progressively_and_ignores_cancelled_streams()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        var translator = new DelayedTranslator();
        var viewModel = Create(store, translator);
        viewModel.SourceText = "hello";

        var first = viewModel.TranslateCommand.ExecuteAsync(null);
        translator.ReportPartial(0, "안");
        await WaitUntilAsync(() => viewModel.TranslatedText == "안");
        Assert.Equal("안", viewModel.TranslatedText);
        Assert.True(viewModel.IsTranslating);

        viewModel.ClearCommand.Execute(null);
        translator.ReportPartial(0, "stale");
        translator.Complete(0, "stale final");
        await first;

        Assert.Empty(viewModel.TranslatedText);
        Assert.False(viewModel.IsTranslating);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
    }

    private static TranslatorViewModel Create(SettingsStore store, ITranslator translator) =>
        new(new TranslateTextUseCase(translator), new ChangeLanguagesUseCase(store), store, new FakeClipboard());

    private sealed class DelayedTranslator : ITranslator
    {
        public List<(TranslationRequest Request, CancellationToken Token, IProgress<string>? Progress, TaskCompletionSource<Result<TranslationResult>> Completion)> Requests { get; } = [];

        public Task<Result<TranslationResult>> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken = default) =>
            TranslateAsync(request, onPartialText: null, cancellationToken);

        public Task<Result<TranslationResult>> TranslateAsync(
            TranslationRequest request,
            IProgress<string>? onPartialText,
            CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<Result<TranslationResult>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Requests.Add((request, cancellationToken, onPartialText, completion));
            return completion.Task;
        }

        public void ReportPartial(int index, string text) => Requests[index].Progress?.Report(text);

        public void Complete(int index, string text)
        {
            var pending = Requests[index];
            pending.Completion.SetResult(new TranslationResult(pending.Request.Text, text, pending.Request.Languages));
        }
    }
}
