using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;

namespace MiniTranslator.Testing;

public sealed class FakeCodexAccount : ICodexAccount
{
    public const string Email = "user@example.com";

    public bool SignedIn { get; set; }

    public Error? FailWith { get; set; }

    public TaskCompletionSource? Browser { get; set; }

    public List<string> Calls { get; } = [];

    public Task<Result<CodexAccountState>> ReadAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        Calls.Add($"read:{executablePath}");
        return Task.FromResult(State());
    }

    public async Task<Result<CodexAccountState>> SignInAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        Calls.Add($"sign-in:{executablePath}");
        if (Browser is { } browser)
        {
            await browser.Task.WaitAsync(cancellationToken);
        }

        SignedIn = FailWith is null;
        return State();
    }

    public Task<Result<CodexAccountState>> SignOutAsync(string executablePath, CancellationToken cancellationToken = default)
    {
        Calls.Add($"sign-out:{executablePath}");
        if (FailWith is null)
        {
            SignedIn = false;
        }

        return Task.FromResult(State());
    }

    private Result<CodexAccountState> State()
    {
        if (FailWith is { } error)
        {
            return error;
        }

        return SignedIn ? new CodexAccountState(true, Email) : CodexAccountState.SignedOut;
    }
}
