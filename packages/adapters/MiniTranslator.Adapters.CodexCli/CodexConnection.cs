using System.Text.Json;
using MiniTranslator.Domain.Common;

namespace MiniTranslator.Adapters.CodexCli;

/// <summary>
/// JSON-RPC over one Codex app-server process: numbers the requests, matches replies to them, and refuses
/// whatever the server asks of this client. Disposing it ends the process.
/// </summary>
internal sealed class CodexConnection(ICodexProcess process) : IDisposable
{
    private static readonly string Version = typeof(CodexConnection).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    private long _lastRequestId;

    public long NextRequestId() => ++_lastRequestId;

    /// <exception cref="IOException">The process is gone.</exception>
    public Task SendAsync(string line, CancellationToken cancellationToken) => process.WriteLineAsync(line, cancellationToken);

    /// <summary>Reads the next message, refusing a server request on the way.</summary>
    /// <returns>The message; <c>null</c> once the process has exited.</returns>
    public async Task<CodexMessage?> ReadAsync(CancellationToken cancellationToken)
    {
        if (await process.ReadLineAsync(cancellationToken) is not { } line)
        {
            return null;
        }

        var message = CodexCliProtocol.Parse(line);
        if (message is CodexMessage.ServerRequest request)
        {
            await process.WriteLineAsync(CodexCliProtocol.Refuse(request.Id), cancellationToken);
        }

        return message;
    }

    /// <summary>Sends one request and waits for its reply.</summary>
    /// <param name="request">Builds the request line for the id it is given.</param>
    /// <param name="rejected">The error for a request the server rejected, given what the server said.</param>
    /// <returns>The reply or the rejection; <c>null</c> when the process ended first.</returns>
    public async Task<Result<JsonElement>?> CallAsync(
        Func<long, string> request,
        Func<string?, Error> rejected,
        CancellationToken cancellationToken)
    {
        var requestId = NextRequestId();
        await SendAsync(request(requestId), cancellationToken);

        while (await ReadAsync(cancellationToken) is { } message)
        {
            switch (message)
            {
                case CodexMessage.Reply reply when reply.Id == requestId:
                    return reply.Result;

                case CodexMessage.Failure failure when failure.Id == requestId:
                    return rejected(failure.Detail);
            }
        }

        return null;
    }

    /// <summary>The handshake every fresh process needs before any other request.</summary>
    /// <returns>Whether the server accepted it; <c>null</c> when the process ended first.</returns>
    public async Task<Result<Unit>?> HandshakeAsync(Func<string?, Error> rejected, CancellationToken cancellationToken)
    {
        if (await CallAsync(id => CodexCliProtocol.Initialize(id, Version), rejected, cancellationToken) is not { } initialized)
        {
            return null;
        }

        if (initialized.IsFailure)
        {
            return initialized.Error;
        }

        await SendAsync(CodexCliProtocol.Initialized, cancellationToken);
        return Unit.Value;
    }

    public void Dispose() => process.Dispose();
}
