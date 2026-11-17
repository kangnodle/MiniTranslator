using MiniTranslator.Domain.Common;

namespace MiniTranslator.Application.Abstractions;

/// <summary>Who the Codex CLI is signed in as, in the profile this app keeps for it.</summary>
/// <param name="SignedIn">Whether translations can run.</param>
/// <param name="Email">The account's e-mail address, when signed in and the CLI reports one.</param>
public sealed record CodexAccountState(bool SignedIn, string? Email = null)
{
    public static CodexAccountState SignedOut { get; } = new(false);
}

/// <summary>
/// The Codex CLI's own sign-in. The CLI keeps the credentials; this app only asks it to sign in or out.
/// Every operation takes the CLI path as currently typed in the settings, which may not be saved yet.
/// </summary>
public interface ICodexAccount
{
    Task<Result<CodexAccountState>> ReadAsync(string executablePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the sign-in page in the browser and waits until it is completed there.
    /// Cancelling abandons the attempt.
    /// </summary>
    Task<Result<CodexAccountState>> SignInAsync(string executablePath, CancellationToken cancellationToken = default);

    Task<Result<CodexAccountState>> SignOutAsync(string executablePath, CancellationToken cancellationToken = default);
}
