using MiniTranslator.Domain.Common;

namespace MiniTranslator.Application.Abstractions;

public sealed record CodexAccountState(bool SignedIn, string? Email = null)
{
    public static CodexAccountState SignedOut { get; } = new(false);
}

public interface ICodexAccount
{
    Task<Result<CodexAccountState>> ReadAsync(string executablePath, CancellationToken cancellationToken = default);

    Task<Result<CodexAccountState>> SignInAsync(string executablePath, CancellationToken cancellationToken = default);

    Task<Result<CodexAccountState>> SignOutAsync(string executablePath, CancellationToken cancellationToken = default);
}
