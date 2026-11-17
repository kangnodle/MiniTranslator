using System.Text.Json;
using MiniTranslator.Domain.Common;

namespace MiniTranslator.Adapters.CodexCli;

internal sealed class CodexConnection(ICodexProcess process) : IDisposable
{
    private static readonly string Version = typeof(CodexConnection).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    private long _lastRequestId;

    public long NextRequestId() => ++_lastRequestId;

    public Task SendAsync(string line, CancellationToken cancellationToken) => process.WriteLineAsync(line, cancellationToken);

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
