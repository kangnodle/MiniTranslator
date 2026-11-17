using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Testing;

namespace MiniTranslator.Application.Tests;

public class SettingsStoreConcurrencyTests
{
    [Fact]
    public async Task Overlapping_updates_save_in_order_and_keep_both_changes()
    {
        var repository = new DelayedSettingsRepository();
        var store = new SettingsStore(repository);
        var published = new List<AppSettings>();
        store.Changed += (_, settings) => published.Add(settings);

        var first = store.UpdateAsync(s => s with { Behavior = s.Behavior with { HideOnDeactivate = false } });
        var japanese = LanguagePair.Create("en", "ja").Value;
        var second = new ChangeLanguagesUseCase(store).ExecuteAsync(japanese);

        Assert.Equal(1, repository.SaveCount);
        Assert.False(second.IsCompleted);
        repository.ReleaseFirstSave.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(japanese, store.Current.Languages);
        Assert.False(store.Current.Behavior.HideOnDeactivate);
        Assert.Equal(store.Current, repository.Saved);
        Assert.Equal(2, published.Count);
        Assert.Equal(store.Current, published[^1]);
    }

    [Fact]
    public async Task Cancelling_a_queued_update_does_not_block_later_saves()
    {
        var repository = new DelayedSettingsRepository();
        var store = new SettingsStore(repository);
        var first = store.UpdateAsync(s => s with { Behavior = s.Behavior with { HideOnDeactivate = false } });
        using var cancellation = new CancellationTokenSource();
        var queued = store.UpdateAsync(s => s with { Languages = LanguagePair.Default.Swap().Value }, cancellation.Token);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        repository.ReleaseFirstSave.SetResult();
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        await store.UpdateAsync(s => s with { Languages = LanguagePair.Default.Swap().Value })
            .WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, repository.SaveCount);
        Assert.Equal(store.Current, repository.Saved);
    }

    [Fact]
    public async Task A_queued_draft_save_preserves_the_language_saved_ahead_of_it()
    {
        var repository = new DelayedSettingsRepository();
        var store = new SettingsStore(repository);
        var original = store.Current;
        var japanese = LanguagePair.Create("en", "ja").Value;
        var first = new ChangeLanguagesUseCase(store).ExecuteAsync(japanese);
        var editor = new SettingsEditor(store, new HotkeyManager(new FakeGlobalHotkey()), new FakeStartupRegistration(), new FakeCodexAccount());
        var draft = original with { Behavior = original.Behavior with { HideOnDeactivate = false } };
        var second = editor.SaveAsync(draft, startWithWindows: false);

        Assert.False(second.IsCompleted);
        repository.ReleaseFirstSave.SetResult();
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(japanese, store.Current.Languages);
        Assert.False(store.Current.Behavior.HideOnDeactivate);
        Assert.Equal(store.Current, repository.Saved);
    }

    private sealed class DelayedSettingsRepository : ISettingsRepository
    {
        public TaskCompletionSource ReleaseFirstSave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int SaveCount { get; private set; }
        public AppSettings? Saved { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AppSettings.Default);

        public async Task<Result<Unit>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            if (++SaveCount == 1)
            {
                await ReleaseFirstSave.Task.WaitAsync(cancellationToken);
            }

            Saved = settings;
            return Result.Success();
        }
    }
}
