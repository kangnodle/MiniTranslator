using System.ComponentModel;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.CodexCli.Tests;

public class CodexCliAccountTests
{
    private const string Path = @"C:\Tools\codex.exe";

    private static readonly CodexAccountState SignedIn = new(true, Sessions.Email);

    private sealed class Fixture
    {
        public Fixture(Sessions sessions, TimeSpan? signInTimeout = null, Action<Uri>? openBrowser = null)
        {
            Sessions = sessions;
            Account = new CodexCliAccount(
                sessions.StartFrom,
                openBrowser ?? Opened.Add,
                _ =>
                {
                    Resets++;
                    return Task.CompletedTask;
                },
                callTimeout: TimeSpan.FromSeconds(30),
                signInTimeout: signInTimeout ?? TimeSpan.FromSeconds(30));
        }

        public Sessions Sessions { get; }

        public CodexCliAccount Account { get; }

        public List<Uri> Opened { get; } = [];

        public int Resets { get; private set; }

        public FakeServer Server => Assert.Single(Sessions.Started);
    }

    [Fact]
    public async Task Reads_who_is_signed_in_from_a_process_it_then_ends()
    {
        var fixture = new Fixture(new Sessions());

        var state = await fixture.Account.ReadAsync(Path);

        Assert.Equal(SignedIn, state.Value);
        Assert.Equal(["initialize", "initialized", "account/read"], fixture.Server.Methods);
        Assert.True(fixture.Server.Disposed);
        Assert.Equal([Path], fixture.Sessions.Paths);
    }

    [Fact]
    public async Task Reads_a_signed_out_profile()
    {
        var fixture = new Fixture(new Sessions { SignedIn = false });

        var state = await fixture.Account.ReadAsync(Path);

        Assert.Equal(CodexAccountState.SignedOut, state.Value);
    }

    [Fact]
    public async Task Sign_in_opens_the_page_waits_for_the_browser_and_restarts_translation()
    {
        var fixture = new Fixture(new Sessions { SignedIn = false });

        var state = await fixture.Account.SignInAsync(Path);

        Assert.Equal(SignedIn, state.Value);
        Assert.Equal(new Uri(fixture.Sessions.LoginPage), Assert.Single(fixture.Opened));
        Assert.Equal(1, fixture.Resets);
        Assert.Equal(["initialize", "initialized", "account/read", "account/login/start", "account/read"], fixture.Server.Methods);
        Assert.True(fixture.Server.Disposed);
    }

    [Fact]
    public async Task Sign_in_does_nothing_when_already_signed_in()
    {
        var fixture = new Fixture(new Sessions());

        var state = await fixture.Account.SignInAsync(Path);

        Assert.Equal(SignedIn, state.Value);
        Assert.Empty(fixture.Opened);
        Assert.Equal(0, fixture.Resets);
        Assert.DoesNotContain("account/login/start", fixture.Server.Methods);
    }

    [Theory]
    [InlineData("http://auth.openai.com/oauth/authorize")]
    [InlineData("https://auth.openai.com.example.org/oauth/authorize")]
    [InlineData("https://auth.openai.com@example.org/oauth/authorize")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("not a url")]
    public async Task Sign_in_never_opens_a_page_that_is_not_the_openai_sign_in_page(string page)
    {
        var fixture = new Fixture(new Sessions { SignedIn = false, LoginPage = page });

        var state = await fixture.Account.SignInAsync(Path);

        Assert.Equal("Translation.CodexSignInFailed", state.Error.Code);
        Assert.Empty(fixture.Opened);
        Assert.Equal(0, fixture.Resets);
        Assert.True(fixture.Server.Disposed);
    }

    [Fact]
    public async Task Sign_in_reports_why_the_browser_step_failed()
    {
        var fixture = new Fixture(new Sessions { SignedIn = false, Login = LoginOutcome.Fails });

        var state = await fixture.Account.SignInAsync(Path);

        Assert.Equal("Translation.CodexSignInFailed", state.Error.Code);
        Assert.Contains("access was denied", state.Error.Message);
        Assert.Equal(0, fixture.Resets);
    }

    [Fact]
    public async Task Sign_in_reports_a_browser_that_cannot_be_opened()
    {
        var fixture = new Fixture(new Sessions { SignedIn = false }, openBrowser: _ => throw new Win32Exception("no browser"));

        var state = await fixture.Account.SignInAsync(Path);

        Assert.Equal("Translation.CodexSignInFailed", state.Error.Code);
        Assert.Contains("no browser", state.Error.Message);
        Assert.True(fixture.Server.Disposed);
    }

    [Fact]
    public async Task Cancelling_a_sign_in_ends_the_process_that_was_waiting_for_the_browser()
    {
        var fixture = new Fixture(new Sessions { SignedIn = false, Login = LoginOutcome.NeverCompletes });
        using var cancellation = new CancellationTokenSource();

        var pending = fixture.Account.SignInAsync(Path, cancellation.Token);
        await fixture.Server.Waiting.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(fixture.Server.Disposed);
        Assert.Equal(0, fixture.Resets);
    }

    [Fact]
    public async Task Sign_in_gives_up_when_the_browser_step_takes_too_long()
    {
        var fixture = new Fixture(
            new Sessions { SignedIn = false, Login = LoginOutcome.NeverCompletes },
            signInTimeout: TimeSpan.FromMilliseconds(50));

        var state = await fixture.Account.SignInAsync(Path);

        Assert.Equal(TranslationErrors.CodexAccountTimedOut, state.Error);
        Assert.True(fixture.Server.Disposed);
    }

    [Fact]
    public async Task Sign_out_signs_the_profile_out_and_restarts_translation()
    {
        var fixture = new Fixture(new Sessions());

        var state = await fixture.Account.SignOutAsync(Path);

        Assert.Equal(CodexAccountState.SignedOut, state.Value);
        Assert.False(fixture.Sessions.SignedIn);
        Assert.Equal(1, fixture.Resets);
        Assert.Equal(["initialize", "initialized", "account/logout", "account/read"], fixture.Server.Methods);
        Assert.True(fixture.Server.Disposed);
    }

    [Fact]
    public async Task Reports_a_missing_cli()
    {
        var account = new CodexCliAccount(
            _ => throw new Win32Exception(2),
            _ => { },
            _ => Task.CompletedTask,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));

        Assert.Equal(TranslationErrors.CodexUnavailable, (await account.ReadAsync(Path)).Error);
        Assert.Equal(TranslationErrors.CodexUnavailable, (await account.SignInAsync(Path)).Error);
        Assert.Equal(TranslationErrors.CodexUnavailable, (await account.SignOutAsync(Path)).Error);
    }

    [Theory]
    [InlineData("initialize")]
    [InlineData("account/read")]
    public async Task Reports_a_process_that_exits_without_answering(string method)
    {
        var fixture = new Fixture(new Sessions { ExitOn = method });

        var state = await fixture.Account.ReadAsync(Path);

        Assert.Equal("Translation.CodexAccountFailed", state.Error.Code);
        Assert.True(fixture.Server.Disposed);
    }
}
