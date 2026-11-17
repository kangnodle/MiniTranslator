using System.Text.Json;

namespace MiniTranslator.Adapters.CodexCli;

/// <summary>One line read from the Codex app-server, reduced to what a translation turn needs.</summary>
internal abstract record CodexMessage
{
    private CodexMessage()
    {
    }

    /// <summary>Anything that does not move a translation forward: other notifications, diagnostics, malformed lines.</summary>
    public static CodexMessage Ignored { get; } = new IgnoredMessage();

    /// <summary>The successful answer to the request this app sent with <paramref name="Id"/>.</summary>
    public sealed record Reply(long Id, JsonElement Result) : CodexMessage;

    /// <summary>The server rejected the request this app sent with <paramref name="Id"/>.</summary>
    public sealed record Failure(long Id, string? Detail) : CodexMessage;

    /// <summary>
    /// The server asks this app for something (an approval, a tool result). A translation session never grants any,
    /// so each must be answered with <see cref="CodexCliProtocol.Refuse"/>.
    /// </summary>
    /// <param name="Id">The request id exactly as the server wrote it: a JSON number or string.</param>
    public sealed record ServerRequest(string Id) : CodexMessage;

    /// <summary>A piece of the model's answer.</summary>
    public sealed record Delta(string ThreadId, string TurnId, string Text) : CodexMessage;

    /// <summary>The turn ended; <paramref name="Detail"/> says why when it did not succeed.</summary>
    public sealed record TurnCompleted(string ThreadId, string TurnId, bool Succeeded, string? Detail) : CodexMessage;

    /// <summary>The browser sign-in started with <see cref="CodexCliProtocol.StartLogin"/> ended.</summary>
    public sealed record LoginCompleted(string LoginId, bool Succeeded, string? Detail) : CodexMessage;

    private sealed record IgnoredMessage : CodexMessage;
}
