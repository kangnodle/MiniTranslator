using System.Text.Json;

namespace MiniTranslator.Adapters.CodexCli.Tests;

internal enum LoginOutcome
{
    Succeeds,
    Fails,
    NeverCompletes,
}

internal sealed class Sessions
{
    public const string Email = "user@example.com";

    public bool SignedIn { get; set; } = true;

    public Func<Turn, string[]?> Turn { get; set; } = FakeServer.Echoing;

    public string LoginPage { get; set; } = "https://auth.openai.com/oauth/authorize?client_id=app&state=abc";

    public LoginOutcome Login { get; set; } = LoginOutcome.Succeeds;

    public string? ExitOn { get; set; }

    public List<FakeServer> Started { get; } = [];

    public List<string> Paths { get; } = [];

    public ICodexProcess Start()
    {
        var server = new FakeServer(this);
        Started.Add(server);
        return server;
    }

    public ICodexProcess StartFrom(string executablePath)
    {
        Paths.Add(executablePath);
        return Start();
    }
}

internal sealed record Turn(long RequestId, string ThreadId, string TurnId, string Text)
{
    public string Reply => JsonSerializer.Serialize(new { id = RequestId, result = new { turn = new { id = TurnId, status = "inProgress" } } });

    public string Completed => Ended("completed", null);

    public string Delta(string text) => JsonSerializer.Serialize(new
    {
        method = "item/agentMessage/delta",
        @params = new { threadId = ThreadId, turnId = TurnId, itemId = "msg_1", delta = text },
    });

    public string Ended(string status, string? error) => JsonSerializer.Serialize(new
    {
        method = "turn/completed",
        @params = new { threadId = ThreadId, turn = new { id = TurnId, status, error = error is null ? null : new { message = error } } },
    });
}

internal sealed class FakeServer(Sessions sessions) : ICodexProcess
{
    public const string LoginId = "login-1";

    private readonly Queue<string> _output = new();
    private readonly TaskCompletionSource _waiting = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _exited;
    private int _threads;
    private int _turns;

    public static Func<Turn, string[]?> Echoing { get; } = turn =>
    [
        turn.Reply,
        JsonSerializer.Serialize(new { method = "turn/started", @params = new { threadId = turn.ThreadId, turn = new { id = turn.TurnId } } }),
        turn.Delta("["),
        turn.Delta(turn.Text),
        turn.Delta("]"),
        turn.Completed,
    ];

    public static Func<Turn, string[]?> Silent { get; } = turn => [turn.Reply];

    public static Func<Turn, string[]?> Failing(string message) => turn => [turn.Reply, turn.Ended("failed", message)];

    public List<string> Methods { get; } = [];

    public List<JsonElement> Refusals { get; } = [];

    public bool Disposed { get; private set; }

    public Task Waiting => _waiting.Task;

    public void Exit() => _exited = true;

    public Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        if (_exited)
        {
            throw new IOException("The pipe has been ended.");
        }

        Assert.DoesNotContain('\n', line);
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        if (!root.TryGetProperty("method", out var method))
        {
            Refusals.Add(root.Clone());
            return Task.CompletedTask;
        }

        Methods.Add(method.GetString()!);
        var lines = method.GetString() == sessions.ExitOn ? null : Respond(method.GetString()!, root);
        if (lines is null)
        {
            _exited = true;
        }
        else
        {
            foreach (var output in lines) _output.Enqueue(output);
        }

        return Task.CompletedTask;
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        if (_output.TryDequeue(out var line))
        {
            return line;
        }

        if (!_exited)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        return null;
    }

    public void Dispose() => Disposed = true;

    private string[]? Respond(string method, JsonElement request)
    {
        switch (method)
        {
            case "initialize":
                return ["WARNING: not json", Result(request, new { userAgent = "fake", platformOs = "windows" })];

            case "account/read":
                var account = sessions.SignedIn ? new { type = "chatgpt", email = Sessions.Email, planType = "plus" } : null;
                return
                [
                    """{"method":"account/updated","params":{"authMode":"chatgpt"}}""",
                    Result(request, new { account, requiresOpenaiAuth = true }),
                ];

            case "account/login/start":
                return [Result(request, new { type = "chatgpt", loginId = LoginId, authUrl = sessions.LoginPage }), .. CompleteLogin()];

            case "account/logout":
                sessions.SignedIn = false;
                return [Result(request, new { })];

            case "thread/start":
                return [Result(request, new { thread = new { id = $"thread-{++_threads}" }, model = "gpt-6.1-terra" })];

            case "turn/start":
                var parameters = request.GetProperty("params");
                var turn = new Turn(
                    Id(request),
                    parameters.GetProperty("threadId").GetString()!,
                    $"turn-{++_turns}",
                    SourceText(parameters.GetProperty("input")[0].GetProperty("text").GetString()!));
                var lines = sessions.Turn(turn);
                _waiting.TrySetResult();
                return lines;

            default:
                return [];
        }
    }

    private string[] CompleteLogin()
    {
        switch (sessions.Login)
        {
            case LoginOutcome.Succeeds:
                sessions.SignedIn = true;
                return
                [
                    LoginCompleted("another-login", success: false, "not this one"),
                    LoginCompleted(LoginId, success: true, null),
                ];

            case LoginOutcome.Fails:
                return [LoginCompleted(LoginId, success: false, "access was denied")];

            default:
                _waiting.TrySetResult();
                return [];
        }
    }

    private static string LoginCompleted(string loginId, bool success, string? error) =>
        JsonSerializer.Serialize(new { method = "account/login/completed", @params = new { loginId, success, error } });

    private static long Id(JsonElement request) => request.GetProperty("id").GetInt64();

    private static string Result(JsonElement request, object result) => JsonSerializer.Serialize(new { id = Id(request), result });

    private static string SourceText(string content)
    {
        var openStart = content.IndexOf("\n<source_text_", StringComparison.Ordinal) + 1;
        var openEnd = content.IndexOf(">\n", openStart, StringComparison.Ordinal);
        var tag = content[(openStart + 1)..openEnd];
        var start = openEnd + 2;
        return content[start..content.LastIndexOf($"\n</{tag}>", StringComparison.Ordinal)];
    }
}
