using System.ComponentModel;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.CodexCli;

internal sealed class CodexCliAccount(
    Func<string, ICodexProcess> startProcess,
    Action<Uri> openBrowser,
    Func<CancellationToken, Task> signInChanged,
    TimeSpan callTimeout,
    TimeSpan signInTimeout) : ICodexAccount
{
    public Task<Result<CodexAccountState>> ReadAsync(string executablePath, CancellationToken cancellationToken = default) =>
        RunAsync(executablePath, callTimeout, ReadAsync, cancellationToken);

    public Task<Result<CodexAccountState>> SignInAsync(string executablePath, CancellationToken cancellationToken = default) =>
        RunAsync(executablePath, signInTimeout, SignInAsync, cancellationToken);

    public Task<Result<CodexAccountState>> SignOutAsync(string executablePath, CancellationToken cancellationToken = default) =>
        RunAsync(executablePath, callTimeout, SignOutAsync, cancellationToken);

    private async Task<Result<CodexAccountState>> RunAsync(
        string executablePath,
        TimeSpan timeout,
        Func<CodexConnection, CancellationToken, Task<Result<CodexAccountState>?>> operation,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        try
        {
            using var connection = new CodexConnection(startProcess(executablePath));

            if (await connection.HandshakeAsync(TranslationErrors.CodexAccountFailed, deadline.Token) is not { } handshake)
            {
                return TranslationErrors.CodexAccountFailed(null);
            }

            if (handshake.IsFailure)
            {
                return handshake.Error;
            }

            return await operation(connection, deadline.Token) ?? TranslationErrors.CodexAccountFailed(null);
        }
        catch (Win32Exception)
        {
            return TranslationErrors.CodexUnavailable;
        }
        catch (IOException)
        {
            return TranslationErrors.CodexAccountFailed(null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TranslationErrors.CodexAccountTimedOut;
        }
    }

    private static async Task<Result<CodexAccountState>?> ReadAsync(CodexConnection connection, CancellationToken cancellationToken)
    {
        if (await connection.CallAsync(CodexCliProtocol.ReadAccount, TranslationErrors.CodexAccountFailed, cancellationToken) is not { } account)
        {
            return null;
        }

        return account.IsFailure ? account.Error : CodexCliProtocol.Account(account.Value);
    }

    private async Task<Result<CodexAccountState>?> SignInAsync(CodexConnection connection, CancellationToken cancellationToken)
    {
        if (await ReadAsync(connection, cancellationToken) is not { } current)
        {
            return null;
        }

        if (current.IsFailure || current.Value.SignedIn)
        {
            return current;
        }

        if (await connection.CallAsync(CodexCliProtocol.StartLogin, TranslationErrors.CodexSignInFailed, cancellationToken) is not { } started)
        {
            return null;
        }

        if (started.IsFailure)
        {
            return started.Error;
        }

        if (CodexCliProtocol.Login(started.Value) is not { } login)
        {
            return TranslationErrors.CodexSignInFailed(null);
        }

        try
        {
            openBrowser(login.Page);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or PlatformNotSupportedException)
        {
            return TranslationErrors.CodexSignInFailed(ex.Message);
        }

        while (await connection.ReadAsync(cancellationToken) is { } message)
        {
            if (message is not CodexMessage.LoginCompleted completed || completed.LoginId != login.Id)
            {
                continue;
            }

            if (!completed.Succeeded)
            {
                return TranslationErrors.CodexSignInFailed(completed.Detail);
            }

            await signInChanged(cancellationToken);
            return await ReadAsync(connection, cancellationToken);
        }

        return null;
    }

    private async Task<Result<CodexAccountState>?> SignOutAsync(CodexConnection connection, CancellationToken cancellationToken)
    {
        if (await connection.CallAsync(CodexCliProtocol.Logout, TranslationErrors.CodexAccountFailed, cancellationToken) is not { } signedOut)
        {
            return null;
        }

        if (signedOut.IsFailure)
        {
            return signedOut.Error;
        }

        await signInChanged(cancellationToken);
        return await ReadAsync(connection, cancellationToken);
    }
}
