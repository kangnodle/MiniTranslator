using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Testing;

namespace MiniTranslator.Application.Tests;

public class SettingsEditorCodexAccountTests
{
    private static (SettingsEditor Editor, FakeCodexAccount Account, InMemorySettingsRepository Repository) Create()
    {
        var account = new FakeCodexAccount();
        var repository = new InMemorySettingsRepository();
        var editor = new SettingsEditor(
            new SettingsStore(repository), new HotkeyManager(new FakeGlobalHotkey()), new FakeStartupRegistration(), account);
        return (editor, account, repository);
    }

    [Fact]
    public async Task Account_requests_use_the_drafted_cli_path_cleaned_like_a_saved_one()
    {
        var (editor, account, _) = Create();

        await editor.ReadCodexAccountAsync(@" ""C:\Tools\codex.exe"" ");
        await editor.SignInToCodexAsync(null);
        await editor.SignOutOfCodexAsync("   ");

        Assert.Equal([@"read:C:\Tools\codex.exe", "sign-in:", "sign-out:"], account.Calls);
    }

    [Fact]
    public async Task Signing_in_and_out_takes_effect_without_saving_the_settings()
    {
        var (editor, account, repository) = Create();

        var signedIn = await editor.SignInToCodexAsync("");
        Assert.Equal(new CodexAccountState(true, FakeCodexAccount.Email), signedIn.Value);
        Assert.True(account.SignedIn);

        var signedOut = await editor.SignOutOfCodexAsync("");
        Assert.Equal(CodexAccountState.SignedOut, signedOut.Value);
        Assert.Equal(0, repository.SaveCount);
    }
}
