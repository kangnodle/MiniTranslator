using System.ComponentModel;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.CodexCli;

/// <summary>
/// Signs the Codex CLI in and out by asking a short-lived app-server to do it, one process per operation.
/// The CLI runs the browser sign-in and stores the credentials; this class never sees a token.
/// </summary>
/// <param name="startProcess">Starts the app-server, given the CLI path typed in the settings.</param>
/// <param name="openBrowser">Shows the sign-in page to the user.</param>
/// <param name="signInChanged">Runs after a sign-in or sign-out, so a running translation session is not left on the old state.</param>
/// <param name="callTimeout">How long reading the account or signing out may take.</param>
/// <param name="signInTimeout">How long the user has to finish signing in in the browser.</param>
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

    /// <summary>Runs one operation on a fresh, handshaken process and turns every way it can break into an error value.</summary>
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
            // The process closed its pipes before answering.
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

        // The page comes from the CLI; only OpenAI's own sign-in page is ever handed to the browser.
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
