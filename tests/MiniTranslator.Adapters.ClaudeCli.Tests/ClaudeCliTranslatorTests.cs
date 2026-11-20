using System.ComponentModel;
using System.Text.Json;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.ClaudeCli.Tests;

public class ClaudeCliTranslatorTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(30);

    private static TranslationRequest Request(string text = "Hello") =>
        TranslationRequest.Create(text, LanguagePair.Default).Value;

    [Fact]
    public async Task Translates_through_the_session()
    {
        var sessions = new Sessions(FakeProcess.Echoing);
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous);

        var result = await translator.TranslateAsync(Request("Hello"));

        Assert.Equal("[Hello]", result.Value.TranslatedText);
        Assert.Equal("Hello", result.Value.SourceText);
    }

    [Fact]
    public async Task Isolates_each_translation_in_its_own_session_by_default()
    {
        var sessions = new Sessions(FakeProcess.Echoing);
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous);

        await translator.TranslateAsync(Request("one"));
        var second = await translator.TranslateAsync(Request("two"));

        Assert.Equal("[two]", second.Value.TranslatedText);
        Assert.Equal(2, sessions.Started.Count);
        Assert.True(sessions.Started[0].Disposed);
        Assert.True(sessions.Started[1].Disposed);
    }

    [Fact]
    public async Task Keeps_one_session_across_translations_when_session_reuse_is_enabled()
    {
        var sessions = new Sessions(FakeProcess.Echoing);
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous, maxTurnsPerSession: 5);

        await translator.TranslateAsync(Request("one"));
        var second = await translator.TranslateAsync(Request("two"));

        Assert.Equal("[two]", second.Value.TranslatedText);
        Assert.Single(sessions.Started);
        Assert.False(sessions.Started[0].Disposed);
    }

    [Fact]
    public void Starts_nothing_until_the_first_translation()
    {
        var sessions = new Sessions(FakeProcess.Echoing);

        using var translator = new ClaudeCliTranslator(sessions.Start, Generous);

        Assert.Empty(sessions.Started);
    }

    [Fact]
    public async Task Starts_a_new_session_with_the_new_model_and_effort_once_they_change()
    {
        var sessions = new Sessions(FakeProcess.Echoing);
        var options = new EngineOptions("opus", "low");
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous, () => options, maxTurnsPerSession: 5);

        await translator.TranslateAsync(Request());
        await translator.TranslateAsync(Request());
        options = new EngineOptions("haiku", "");
        await translator.TranslateAsync(Request());

        Assert.Equal([new EngineOptions("opus", "low"), new EngineOptions("haiku", "")], sessions.Options);
        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Replaces_the_session_after_the_turn_limit()
    {
        var sessions = new Sessions(FakeProcess.Echoing);
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous, maxTurnsPerSession: 3);

        for (var turn = 0; turn < 3; turn++)
        {
            await translator.TranslateAsync(Request());
        }

        Assert.Single(sessions.Started);
        Assert.True(sessions.Started[0].Disposed);

        var next = await translator.TranslateAsync(Request("fresh"));

        Assert.Equal("[fresh]", next.Value.TranslatedText);
        Assert.Equal(2, sessions.Started.Count);
        Assert.True(sessions.Started[0].Disposed);
        Assert.Equal(3, sessions.Started[0].Received.Count);
    }

    [Fact]
    public async Task Restarts_once_when_an_idle_session_has_died()
    {
        var sessions = new Sessions(FakeProcess.Echoing);
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous, maxTurnsPerSession: 5);
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
        var sessions = new Sessions(_ => null);
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous);

        var result = await translator.TranslateAsync(Request());

        Assert.Equal("Translation.ClaudeFailed", result.Error.Code);
        Assert.Single(sessions.Started);
        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Reports_the_cli_error_and_starts_over_next_time()
    {
        var respond = FakeProcess.Failing("Not logged in");
        var sessions = new Sessions(text => respond(text));
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous);

        var failed = await translator.TranslateAsync(Request());
        respond = FakeProcess.Echoing;
        var recovered = await translator.TranslateAsync(Request("again"));

        Assert.Equal("Translation.ClaudeFailed", failed.Error.Code);
        Assert.Contains("Not logged in", failed.Error.Message);
        Assert.True(sessions.Started[0].Disposed);
        Assert.Equal("[again]", recovered.Value.TranslatedText);
        Assert.Equal(2, sessions.Started.Count);
    }

    [Fact]
    public async Task Reports_a_missing_cli()
    {
        using var translator = new ClaudeCliTranslator(_ => throw new Win32Exception(2), Generous);

        var result = await translator.TranslateAsync(Request());

        Assert.Equal(TranslationErrors.ClaudeUnavailable, result.Error);
    }

    [Fact]
    public async Task Times_out_and_drops_the_stuck_session()
    {
        var sessions = new Sessions(FakeProcess.Silent);
        using var translator = new ClaudeCliTranslator(sessions.Start, TimeSpan.FromMilliseconds(50));

        var result = await translator.TranslateAsync(Request());

        Assert.Equal(TranslationErrors.ClaudeTimedOut, result.Error);
        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Cancelling_drops_the_session_so_a_late_answer_cannot_leak_into_the_next_request()
    {
        var respond = FakeProcess.Silent;
        var sessions = new Sessions(text => respond(text));
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous);
        using var cancellation = new CancellationTokenSource();

        var pending = translator.TranslateAsync(Request("abandoned"), cancellation.Token);
        await sessions.Started[0].FirstWrite.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(sessions.Started[0].Disposed);

        respond = FakeProcess.Echoing;
        var next = await translator.TranslateAsync(Request("next"));

        Assert.Equal("[next]", next.Value.TranslatedText);
        Assert.Equal(2, sessions.Started.Count);
    }

    [Fact]
    public async Task Disposing_ends_the_session()
    {
        var sessions = new Sessions(FakeProcess.Echoing);
        var translator = new ClaudeCliTranslator(sessions.Start, Generous);
        await translator.TranslateAsync(Request());

        translator.Dispose();

        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Disposes_idle_session_after_idle_timeout()
    {
        var sessions = new Sessions(FakeProcess.Echoing);
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous, idleTimeout: TimeSpan.FromMilliseconds(40), maxTurnsPerSession: 5);

        await translator.TranslateAsync(Request("one"));
        Assert.False(sessions.Started[0].Disposed);

        await WaitUntilAsync(() => sessions.Started[0].Disposed);
        Assert.True(sessions.Started[0].Disposed);

        var next = await translator.TranslateAsync(Request("two"));
        Assert.Equal("[two]", next.Value.TranslatedText);
        Assert.Equal(2, sessions.Started.Count);
    }

    [Fact]
    public async Task Releasing_session_immediately_disposes_the_active_process()
    {
        var sessions = new Sessions(FakeProcess.Echoing);
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous, maxTurnsPerSession: 5);
        await translator.TranslateAsync(Request("one"));

        await translator.ReleaseSessionAsync();

        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Rejects_oversized_streaming_output()
    {
        var oversized = new string('x', ClaudeCliTranslator.MaxOutputLength + 1);
        var sessions = new Sessions(_ =>
        [
            JsonSerializer.Serialize(new { type = "stream_event", @event = new { type = "content_block_delta", delta = new { type = "text_delta", text = oversized } } }),
        ]);
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous);

        var result = await translator.TranslateAsync(Request("Hello"), new SyncProgress(_ => { }));

        Assert.Equal("Translation.ClaudeFailed", result.Error.Code);
        Assert.True(sessions.Started[0].Disposed);
    }

    [Fact]
    public async Task Reports_partial_stream_events_before_the_final_result()
    {
        var sessions = new Sessions(text =>
        [
            """{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"text_delta","text":"안"}}}""",
            """{"type":"stream_event","event":{"type":"content_block_delta","delta":{"type":"text_delta","text":"녕"}}}""",
            JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = $"[{text}]" }),
        ]);
        using var translator = new ClaudeCliTranslator(sessions.Start, Generous);
        var updates = new List<string>();
        var progress = new SyncProgress(updates.Add);

        var result = await translator.TranslateAsync(Request("Hello"), progress);

        Assert.Equal("[Hello]", result.Value.TranslatedText);
        Assert.Equal(["안", "안녕"], updates);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(15);
        }
    }

    private sealed class SyncProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }

    private sealed class Sessions(Func<string, string[]?> respond)
    {
        public List<FakeProcess> Started { get; } = [];

        public List<EngineOptions> Options { get; } = [];

        public IClaudeProcess Start(EngineOptions options)
        {
            var process = new FakeProcess(respond);
            Started.Add(process);
            Options.Add(options);
            return process;
        }
    }

    private sealed class FakeProcess(Func<string, string[]?> respond) : IClaudeProcess
    {
        private readonly Queue<string> _output = new();
        private readonly TaskCompletionSource _firstWrite = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _exited;

        public static Func<string, string[]?> Echoing { get; } = text =>
        [
            """{"type":"system","subtype":"init"}""",
            JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = false, result = $"[{text}]" }),
        ];

        public static Func<string, string[]?> Silent { get; } = _ => [];

        public static Func<string, string[]?> Failing(string message) => _ =>
            [JsonSerializer.Serialize(new { type = "result", subtype = "success", is_error = true, result = message })];

        public List<string> Received { get; } = [];

        public bool Disposed { get; private set; }

        public Task FirstWrite => _firstWrite.Task;

        public void Exit() => _exited = true;

        public Task WriteLineAsync(string line, CancellationToken cancellationToken)
        {
            if (_exited)
            {
                throw new IOException("The pipe has been ended.");
            }

            Received.Add(line);
            var lines = respond(SourceText(line));
            if (lines is null)
            {
                _exited = true;
            }
            else
            {
                foreach (var output in lines) _output.Enqueue(output);
            }

            _firstWrite.TrySetResult();
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

        private static string SourceText(string message)
        {
            using var document = JsonDocument.Parse(message);
            var content = document.RootElement.GetProperty("message").GetProperty("content").GetString()!;
            var openStart = content.IndexOf("\n<source_text_", StringComparison.Ordinal) + 1;
            var openEnd = content.IndexOf(">\n", openStart, StringComparison.Ordinal);
            var tag = content[(openStart + 1)..openEnd];
            var start = openEnd + 2;
            return content[start..content.LastIndexOf($"\n</{tag}>", StringComparison.Ordinal)];
        }
    }
}
