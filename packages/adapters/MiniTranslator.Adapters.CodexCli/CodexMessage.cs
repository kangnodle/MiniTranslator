using System.Text.Json;

namespace MiniTranslator.Adapters.CodexCli;

internal abstract record CodexMessage
{
    private CodexMessage()
    {
    }

    public static CodexMessage Ignored { get; } = new IgnoredMessage();

    public sealed record Reply(long Id, JsonElement Result) : CodexMessage;

    public sealed record Failure(long Id, string? Detail) : CodexMessage;

    public sealed record ServerRequest(string Id) : CodexMessage;

    public sealed record Delta(string ThreadId, string TurnId, string Text) : CodexMessage;

    public sealed record TurnCompleted(string ThreadId, string TurnId, bool Succeeded, string? Detail) : CodexMessage;

    public sealed record LoginCompleted(string LoginId, bool Succeeded, string? Detail) : CodexMessage;

    private sealed record IgnoredMessage : CodexMessage;
}
