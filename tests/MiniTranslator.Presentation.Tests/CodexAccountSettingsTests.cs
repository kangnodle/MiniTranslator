using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Testing;

namespace MiniTranslator.Presentation.Tests;

public class CodexAccountSettingsTests
{
    private static (SettingsViewModel ViewModel, FakeCodexAccount Account, InMemorySettingsRepository Repository) Create(
        FakeCodexAccount? account = null,
        AppSettings? saved = null)
    {
        account ??= new FakeCodexAccount();
        var repository = new InMemorySettingsRepository(saved);
        var store = new SettingsStore(repository);
        if (saved is not null)
        {
            store.InitializeAsync().GetAwaiter().GetResult();
        }

        var hotkeys = new HotkeyManager(new FakeGlobalHotkey());
        hotkeys.Apply(store.Current.Hotkey);
        var editor = new SettingsEditor(store, hotkeys, new FakeStartupRegistration(), account);
        return (new SettingsViewModel(editor), account, repository);
    }

    [Fact]
    public void Nothing_is_asked_of_codex_while_another_engine_is_selected()
    {
        var (vm, account, _) = Create();

        vm.Engine = TranslationEngine.Claude;

        Assert.Empty(account.Calls);
        Assert.Null(vm.CodexAccountText);
    }

    [Fact]
    public void Choosing_codex_shows_who_is_signed_in()
    {
        var (vm, account, _) = Create(new FakeCodexAccount { SignedIn = true });

        vm.Engine = TranslationEngine.Codex;

        Assert.Equal(["read:"], account.Calls);
        Assert.True(vm.IsCodexSignedIn);
        Assert.False(vm.CanOfferCodexSignIn);
        Assert.Equal($"로그인됨: {FakeCodexAccount.Email}", vm.CodexAccountText);
        Assert.Null(vm.CodexAccountError);
    }

    [Fact]
    public void Opening_settings_with_codex_already_selected_checks_the_sign_in()
    {
        var (vm, account, _) = Create(saved: AppSettings.Default with { Engine = TranslationEngine.Codex, CodexExecutablePath = @"C:\Tools\codex.exe" });

        Assert.Equal([@"read:C:\Tools\codex.exe"], account.Calls);
        Assert.False(vm.IsCodexSignedIn);
        Assert.True(vm.CanOfferCodexSignIn);
        Assert.Equal("로그인되어 있지 않습니다.", vm.CodexAccountText);
    }

    [Fact]
    public async Task Sign_in_uses_the_path_as_typed_before_it_is_saved()
    {
        var (vm, account, repository) = Create();
        vm.Engine = TranslationEngine.Codex;

        vm.CodexExecutablePath = @" ""C:\Tools\codex.exe"" ";
        await vm.SignInToCodexCommand.ExecuteAsync(null);

        Assert.Equal(@"sign-in:C:\Tools\codex.exe", account.Calls[^1]);
        Assert.True(vm.IsCodexSignedIn);
        Assert.Equal($"로그인됨: {FakeCodexAccount.Email}", vm.CodexAccountText);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task While_the_browser_step_is_open_sign_in_can_only_be_cancelled()
    {
        var (vm, account, _) = Create(new FakeCodexAccount { Browser = new TaskCompletionSource() });
        vm.Engine = TranslationEngine.Codex;

        var pending = vm.SignInToCodexCommand.ExecuteAsync(null);

        Assert.True(vm.IsCodexSigningIn);
        Assert.False(vm.CanOfferCodexSignIn);
        Assert.False(vm.SignInToCodexCommand.CanExecute(null));
        Assert.False(vm.SignOutOfCodexCommand.CanExecute(null));
        Assert.True(vm.SignInToCodexCancelCommand.CanExecute(null));
        Assert.StartsWith("브라우저에서 로그인을 완료하세요", vm.CodexAccountText);

        account.Browser!.SetResult();
        await pending;

        Assert.False(vm.IsCodexSigningIn);
        Assert.True(vm.IsCodexSignedIn);
        Assert.True(vm.SignOutOfCodexCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancelling_the_browser_step_leaves_codex_signed_out_without_an_error()
    {
        var (vm, account, _) = Create(new FakeCodexAccount { Browser = new TaskCompletionSource() });
        vm.Engine = TranslationEngine.Codex;
        var pending = vm.SignInToCodexCommand.ExecuteAsync(null);

        vm.SignInToCodexCancelCommand.Execute(null);
        await pending;

        Assert.False(account.SignedIn);
        Assert.False(vm.IsCodexSignedIn);
        Assert.False(vm.IsCodexSigningIn);
        Assert.True(vm.CanOfferCodexSignIn);
        Assert.Null(vm.CodexAccountError);
        Assert.Equal("로그인되어 있지 않습니다.", vm.CodexAccountText);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Leaving_the_settings_abandons_an_open_browser_step(bool save)
    {
        var (vm, account, _) = Create(new FakeCodexAccount { Browser = new TaskCompletionSource() });
        vm.Engine = TranslationEngine.Codex;
        var pending = vm.SignInToCodexCommand.ExecuteAsync(null);

        if (save)
        {
            await vm.SaveCommand.ExecuteAsync(null);
        }
        else
        {
            vm.CancelCommand.Execute(null);
        }

        await pending;

        Assert.False(account.SignedIn);
        Assert.False(vm.IsCodexSigningIn);
    }

    [Fact]
    public async Task Sign_out_shows_the_signed_out_state()
    {
        var (vm, account, _) = Create(new FakeCodexAccount { SignedIn = true });
        vm.Engine = TranslationEngine.Codex;

        await vm.SignOutOfCodexCommand.ExecuteAsync(null);

        Assert.Equal("sign-out:", account.Calls[^1]);
        Assert.False(vm.IsCodexSignedIn);
        Assert.True(vm.CanOfferCodexSignIn);
        Assert.Equal("로그인되어 있지 않습니다.", vm.CodexAccountText);
    }

    [Fact]
    public async Task A_failed_request_shows_the_error_and_keeps_the_last_known_state()
    {
        var (vm, account, _) = Create(new FakeCodexAccount { SignedIn = true });
        vm.Engine = TranslationEngine.Codex;

        account.FailWith = new Error("Translation.CodexAccountFailed", "Codex 로그인 상태를 확인하거나 바꾸지 못했습니다. 다시 시도하세요.");
        await vm.SignOutOfCodexCommand.ExecuteAsync(null);

        Assert.Equal(account.FailWith.Message, vm.CodexAccountError);
        Assert.Null(vm.CodexAccountText);
        Assert.True(vm.IsCodexSignedIn);
        Assert.True(vm.SignOutOfCodexCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_retry_clears_the_earlier_error()
    {
        var (vm, account, _) = Create(new FakeCodexAccount { FailWith = new Error("Translation.CodexUnavailable", "Codex CLI를 실행할 수 없습니다.") });
        vm.Engine = TranslationEngine.Codex;
        Assert.NotNull(vm.CodexAccountError);

        account.FailWith = null;
        await vm.SignInToCodexCommand.ExecuteAsync(null);

        Assert.Null(vm.CodexAccountError);
        Assert.True(vm.IsCodexSignedIn);
    }

    [Fact]
    public void Account_text_follows_the_ui_language()
    {
        var (vm, _, _) = Create(new FakeCodexAccount { SignedIn = true }, AppSettings.Default with { UiLanguage = UiLanguage.English });

        vm.Engine = TranslationEngine.Codex;

        Assert.Equal($"Signed in as {FakeCodexAccount.Email}", vm.CodexAccountText);
    }
}
