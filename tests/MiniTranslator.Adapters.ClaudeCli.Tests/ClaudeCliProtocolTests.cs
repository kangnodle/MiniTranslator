using System.Text.Json;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.ClaudeCli.Tests;

public class ClaudeCliProtocolTests
{
    private static TranslationRequest Request(string text = "Hello", string source = "en", string target = "ko") =>
        TranslationRequest.Create(text, LanguagePair.Create(source, target).Value).Value;

    private static string Content(TranslationRequest request)
    {
        using var message = JsonDocument.Parse(ClaudeCliProtocol.BuildMessage(request));
        Assert.Equal("user", message.RootElement.GetProperty("type").GetString());
        Assert.Equal("user", message.RootElement.GetProperty("message").GetProperty("role").GetString());
        return message.RootElement.GetProperty("message").GetProperty("content").GetString()!;
    }

    [Fact]
    public void Message_names_both_languages_and_wraps_the_text()
    {
        var content = Content(Request("Good morning"));

        Assert.Contains("from English into Korean", content);
        Assert.Contains("<source_text>\nGood morning\n</source_text>", content);
    }

    [Fact]
    public void Message_asks_for_detection_when_the_source_is_automatic()
    {
        var content = Content(Request(source: "auto"));

        Assert.Contains("into Korean, detecting its language", content);
        Assert.DoesNotContain("from ", content);
    }

    [Fact]
    public void Message_is_a_single_line_whatever_the_text_contains()
    {
        var request = Request("첫 줄\n\"둘째\" 줄\r\n{\"type\":\"result\"}");

        var message = ClaudeCliProtocol.BuildMessage(request);

        Assert.DoesNotContain('\n', message);
        Assert.DoesNotContain('\r', message);
        Assert.Contains(request.Text, Content(request));
    }

    [Fact]
    public void Session_uses_the_chosen_model_and_effort_and_falls_back_to_the_defaults()
    {
        var chosen = ClaudeCliProtocol.Arguments(new EngineOptions("opus", "low")).ToList();
        var defaults = ClaudeCliProtocol.Arguments(EngineOptions.Default).ToList();

        Assert.Equal("opus", chosen[chosen.IndexOf("--model") + 1]);
        Assert.Equal("low", chosen[chosen.IndexOf("--effort") + 1]);
        Assert.Equal(ClaudeCliProtocol.DefaultModel, defaults[defaults.IndexOf("--model") + 1]);
        Assert.DoesNotContain("--effort", defaults);
    }

    [Fact]
    public void Session_runs_without_tools_servers_commands_or_user_settings()
    {
        var arguments = ClaudeCliProtocol.Arguments(EngineOptions.Default).ToList();

        Assert.Equal("", arguments[arguments.IndexOf("--tools") + 1]);
        Assert.Equal("", arguments[arguments.IndexOf("--setting-sources") + 1]);
        Assert.Contains("--strict-mcp-config", arguments);
        Assert.Contains("--disable-slash-commands", arguments);
        Assert.Contains("--no-session-persistence", arguments);
        Assert.DoesNotContain(arguments, argument => argument.Contains("dangerously", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("--mcp-config", arguments);
    }

    [Theory]
    [InlineData("""{"type":"system","subtype":"init"}""")]
    [InlineData("""{"type":"assistant","message":{"content":[{"type":"text","text":"안녕하세요"}]}}""")]
    [InlineData("""["result"]""")]
    [InlineData("warning: not json")]
    [InlineData("")]
    public void Lines_that_do_not_end_the_turn_are_skipped(string line)
    {
        Assert.Null(ClaudeCliProtocol.ParseLine(line, Request()));
    }

    [Fact]
    public void Successful_result_becomes_the_translation()
    {
        var request = Request();

        var result = ClaudeCliProtocol.ParseLine(
            """{"type":"result","subtype":"success","is_error":false,"result":"  안녕하세요\n"}""", request);

        Assert.NotNull(result);
        Assert.Equal(new TranslationResult("Hello", "안녕하세요", request.Languages), result.Value);
    }

    [Theory]
    [InlineData("""{"type":"result","subtype":"success","is_error":true,"result":"Not logged in"}""")]
    [InlineData("""{"type":"result","subtype":"error_during_execution","is_error":false,"result":"Not logged in"}""")]
    public void Failed_result_reports_what_the_cli_said(string line)
    {
        var result = ClaudeCliProtocol.ParseLine(line, Request());

        Assert.NotNull(result);
        Assert.Equal("Translation.ClaudeFailed", result.Error.Code);
        Assert.EndsWith("\nNot logged in", result.Error.Message);
    }

    [Theory]
    [InlineData("""{"type":"result","subtype":"success","is_error":false,"result":"   "}""")]
    [InlineData("""{"type":"result","subtype":"success","is_error":false}""")]
    public void Blank_result_is_an_empty_response(string line)
    {
        var result = ClaudeCliProtocol.ParseLine(line, Request());

        Assert.NotNull(result);
        Assert.Equal(TranslationErrors.EmptyResponse, result.Error);
    }
}

file static class ListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> list, string value) => list.ToList().IndexOf(value);
}
