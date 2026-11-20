using System.ComponentModel;
using System.Text.Json;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.CodexCli.Tests;

public class CodexCliTranslatorTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(30);

    private static TranslationRequest Request(string text = "Hello") =>
        TranslationRequest.Create(text, LanguagePair.Default).Value;

    private static CodexCliTranslator Translator(Sessions sessions, TimeSpan? timeout = null) =>
        new(sessions.Start, timeout ?? Generous);

    [Fact]
    public async Task Translates_by_joining_the_turn_output()
    {
        var sessions = new Sessions();
        using var translator = Translator(sessions);

        var result = await translator.TranslateAsync(Request("Hello"));

        Assert.Equal("[Hello]", result.Value.TranslatedText);
        Assert.Equal("Hello", result.Value.SourceText);
    }

    [Fact]
    public async Task Opens_a_session_by_handshake_sign_in_check_and_thread_before_the_first_turn()
    {
        var sessions = new Sessions();
        using var translator = Translator(sessions);

        await translator.TranslateAsync(Request());

        Assert.Equal(
            ["initialize", "initialized", "account/read", "thread/start", "turn/start"],
            sessions.Started[0].Methods);
    }

    [Fact]
    public async Task Keeps_one_process_and_one_thread_across_translations()
    {
        var sessions = new Sessions();
        using var translator = Translator(sessions);

        await translator.TranslateAsync(Request("one"));
        var second = await translator.TranslateAsync(Request("two"));

        Assert.Equal("[two]", second.Value.TranslatedText);
        Assert.Single(sessions.Started);
        Assert.False(sessions.Started[0].Disposed);
        Assert.Single(sessions.Started[0].Methods, method => method == "thread/start");
        Assert.Single(sessions.Started[0].Methods, method => method == "account/read");
        Assert.Equal(2, sessions.Started[0].Methods.Count(method => method == "turn/start"));
    }

    [Fact]
    public void Starts_nothing_until_the_first_translation()
    {
        var sessions = new Sessions();

        using var translator = Translator(sessions);

        Assert.Empty(sessions.Started);
    }

    [Fact]
    public async Task Signed_out_reports_how_to_sign_in_without_starting_a_thread()
    {
        var sessions = new Sessions { SignedIn = false };
        using var translator = Translator(sessions);

        var result = await translator.TranslateAsync(Request());

        Assert.Equal(TranslationErrors.CodexSignedOut, result.Error);
        Assert.DoesNotContain("thread/start", sessions.Started[0].Methods);
        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Checks_sign_in_again_after_being_signed_out()
    {
        var sessions = new Sessions { SignedIn = false };
        using var translator = Translator(sessions);
        await translator.TranslateAsync(Request());

        sessions.SignedIn = true;
        var result = await translator.TranslateAsync(Request("now"));

        Assert.Equal("[now]", result.Value.TranslatedText);
        Assert.Equal(2, sessions.Started.Count);
    }

    [Fact]
    public async Task Replaces_the_session_after_the_turn_limit()
    {
        var sessions = new Sessions();
        using var translator = Translator(sessions);

        for (var turn = 0; turn < CodexCliTranslator.MaxTurnsPerSession; turn++)
        {
            await translator.TranslateAsync(Request());
        }

        Assert.Single(sessions.Started);

        var next = await translator.TranslateAsync(Request("fresh"));

        Assert.Equal("[fresh]", next.Value.TranslatedText);
        Assert.Equal(2, sessions.Started.Count);
        Assert.True(sessions.Started[0].Disposed);
        Assert.Equal(CodexCliTranslator.MaxTurnsPerSession, sessions.Started[0].Methods.Count(method => method == "turn/start"));
    }

    [Fact]
    public async Task Restarts_once_when_an_idle_session_has_died()
    {
        var sessions = new Sessions();
        using var translator = Translator(sessions);
        await translator.TranslateAsync(Request("one"));
        sessions.Started[0].Exit();

        var result = await translator.TranslateAsync(Request("two"));

        Assert.Equal("[two]", result.Value.TranslatedText);
        Assert.Equal(2, sessions.Started.Count);
        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Fails_without_retrying_when_a_fresh_session_exits_silently()
    {
        var sessions = new Sessions { Turn = _ => null };
        using var translator = Translator(sessions);

        var result = await translator.TranslateAsync(Request());

        Assert.Equal("Translation.CodexFailed", result.Error.Code);
        Assert.Single(sessions.Started);
        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Reports_why_a_turn_failed_and_starts_over_next_time()
    {
        var sessions = new Sessions { Turn = FakeServer.Failing("usage limit reached") };
        using var translator = Translator(sessions);

        var failed = await translator.TranslateAsync(Request());
        sessions.Turn = FakeServer.Echoing;
        var recovered = await translator.TranslateAsync(Request("again"));

        Assert.Equal("Translation.CodexFailed", failed.Error.Code);
        Assert.Contains("usage limit reached", failed.Error.Message);
        Assert.True(sessions.Started[0].Disposed);
        Assert.Equal("[again]", recovered.Value.TranslatedText);
        Assert.Equal(2, sessions.Started.Count);
    }

    [Fact]
    public async Task Reports_a_turn_the_server_rejected()
    {
        var sessions = new Sessions
        {
            Turn = turn => [JsonSerializer.Serialize(new { id = turn.RequestId, error = new { code = -32600, message = "thread not found" } })],
        };
        using var translator = Translator(sessions);

        var result = await translator.TranslateAsync(Request());

        Assert.Equal("Translation.CodexFailed", result.Error.Code);
        Assert.Contains("thread not found", result.Error.Message);
        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Blank_output_is_an_empty_response()
    {
        var sessions = new Sessions { Turn = turn => [turn.Reply, turn.Delta("  "), turn.Completed] };
        using var translator = Translator(sessions);

        var result = await translator.TranslateAsync(Request());

        Assert.Equal(TranslationErrors.EmptyResponse, result.Error);
    }

    [Fact]
    public async Task Refuses_what_the_server_asks_for_and_still_finishes_the_turn()
    {
        var sessions = new Sessions
        {
            Turn = turn =>
            [
                turn.Reply,
                """{"id":"approval-1","method":"item/commandExecution/requestApproval","params":{"threadId":"thread-1"}}""",
                turn.Delta("done"),
                turn.Completed,
            ],
        };
        using var translator = Translator(sessions);

        var result = await translator.TranslateAsync(Request());

        Assert.Equal("done", result.Value.TranslatedText);
        var refusal = Assert.Single(sessions.Started[0].Refusals);
        Assert.Equal("\"approval-1\"", refusal.GetProperty("id").GetRawText());
        Assert.True(refusal.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task Ignores_output_of_other_threads_and_turns()
    {
        var sessions = new Sessions
        {
            Turn = turn =>
            [
                turn.Reply,
                (turn with { ThreadId = "other-thread" }).Delta("A"),
                (turn with { TurnId = "other-turn" }).Delta("B"),
                (turn with { ThreadId = "other-thread" }).Ended("failed", null),
                (turn with { TurnId = "other-turn" }).Ended("failed", null),
                turn.Delta("mine"),
                turn.Completed,
            ],
        };
        using var translator = Translator(sessions);

        var result = await translator.TranslateAsync(Request());

        Assert.Equal("mine", result.Value.TranslatedText);
    }

    [Fact]
    public async Task Keeps_output_that_arrives_before_the_turn_reply()
    {
        var sessions = new Sessions { Turn = turn => [turn.Delta("early "), turn.Reply, turn.Delta("late"), turn.Completed] };
        using var translator = Translator(sessions);

        var result = await translator.TranslateAsync(Request());

        Assert.Equal("early late", result.Value.TranslatedText);
    }

    [Fact]
    public async Task Reports_a_missing_cli()
    {
        using var translator = new CodexCliTranslator(() => throw new Win32Exception(2), Generous);

        var result = await translator.TranslateAsync(Request());

        Assert.Equal(TranslationErrors.CodexUnavailable, result.Error);
    }

    [Fact]
    public async Task Times_out_and_drops_the_stuck_session()
    {
        var sessions = new Sessions { Turn = FakeServer.Silent };
        using var translator = Translator(sessions, TimeSpan.FromMilliseconds(50));

        var result = await translator.TranslateAsync(Request());

        Assert.Equal(TranslationErrors.CodexTimedOut, result.Error);
        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Cancelling_drops_the_session_so_a_late_answer_cannot_leak_into_the_next_request()
    {
        var sessions = new Sessions { Turn = FakeServer.Silent };
        using var translator = Translator(sessions);
        using var cancellation = new CancellationTokenSource();

        var pending = translator.TranslateAsync(Request("abandoned"), cancellation.Token);
        await sessions.Started[0].Waiting.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(sessions.Started[0].Disposed);

        sessions.Turn = FakeServer.Echoing;
        var next = await translator.TranslateAsync(Request("next"));

        Assert.Equal("[next]", next.Value.TranslatedText);
        Assert.Equal(2, sessions.Started.Count);
    }

    [Fact]
    public async Task Resetting_ends_the_session_so_the_next_translation_sees_the_current_sign_in()
    {
        var sessions = new Sessions();
        using var translator = Translator(sessions);
        await translator.TranslateAsync(Request("one"));

        await translator.ResetAsync();
        sessions.SignedIn = false;
        var result = await translator.TranslateAsync(Request("two"));

        Assert.True(sessions.Started[0].Disposed);
        Assert.Equal(TranslationErrors.CodexSignedOut, result.Error);
        Assert.Equal(2, sessions.Started.Count);
    }

    [Fact]
    public async Task Disposing_ends_the_session()
    {
        var sessions = new Sessions();
        var translator = Translator(sessions);
        await translator.TranslateAsync(Request());

        translator.Dispose();

        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Disposes_idle_session_after_idle_timeout()
    {
        var sessions = new Sessions();
        using var translator = new CodexCliTranslator(sessions.Start, Generous, idleTimeout: TimeSpan.FromMilliseconds(40));

        await translator.TranslateAsync(Request("one"));
        Assert.False(sessions.Started[0].Disposed);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!sessions.Started[0].Disposed && DateTime.UtcNow < deadline)
        {
            await Task.Delay(15);
        }

        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Streams_deltas_through_progress_before_completion()
    {
        var sessions = new Sessions
        {
            Turn = turn => [turn.Reply, turn.Delta("안"), turn.Delta("녕"), turn.Completed],
        };
        using var translator = Translator(sessions);
        var updates = new List<string>();
        var progress = new SyncProgress(updates.Add);

        var result = await translator.TranslateAsync(Request("Hello"), progress);

        Assert.Equal("안녕", result.Value.TranslatedText);
        Assert.Equal(["안", "안녕"], updates);
    }

    private sealed class SyncProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
