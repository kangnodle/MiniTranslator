using System.Text.Json;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.CodexCli.Tests;

public class CodexCliProtocolTests
{
    private static TranslationRequest Request(string text = "Hello", string source = "en", string target = "ko") =>
        TranslationRequest.Create(text, LanguagePair.Create(source, target).Value).Value;

    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement Parameters(string line, long id, string method)
    {
        var root = Json(line);
        Assert.Equal(id, root.GetProperty("id").GetInt64());
        Assert.Equal(method, root.GetProperty("method").GetString());
        return root.GetProperty("params");
    }

    private static string TurnText(TranslationRequest request)
    {
        var input = Parameters(CodexCliProtocol.StartTurn(4, "thread-1", request), 4, "turn/start").GetProperty("input");
        Assert.Equal(1, input.GetArrayLength());
        Assert.Equal("text", input[0].GetProperty("type").GetString());
        return input[0].GetProperty("text").GetString()!;
    }

    [Fact]
    public void Server_runs_on_stdio_and_names_no_feature_a_newer_cli_could_reject()
    {
        Assert.Equal(["app-server", "--listen", "stdio://"], CodexCliProtocol.Arguments);
    }

    [Theory]
    [InlineData("shell_tool")]
    [InlineData("unified_exec")]
    [InlineData("apps")]
    [InlineData("plugins")]
    [InlineData("multi_agent")]
    [InlineData("browser_use")]
    [InlineData("computer_use")]
    public void Thread_has_no_agent_capabilities(string feature)
    {
        var config = Parameters(CodexCliProtocol.StartThread(3, @"C:\Temp\work"), 3, "thread/start").GetProperty("config");

        Assert.False(config.GetProperty($"features.{feature}").GetBoolean());
    }

    [Fact]
    public void Thread_switches_features_off_only()
    {
        var config = Parameters(CodexCliProtocol.StartThread(3, @"C:\Temp\work"), 3, "thread/start").GetProperty("config");

        var features = config.EnumerateObject().Where(entry => entry.Name.StartsWith("features.", StringComparison.Ordinal)).ToList();

        Assert.Equal(CodexCliProtocol.DisabledFeatures.Count, features.Count);
        Assert.All(features, feature => Assert.Equal(JsonValueKind.False, feature.Value.ValueKind));
    }

    [Fact]
    public void Sign_in_cannot_be_replaced_by_an_api_key_or_another_endpoint()
    {
        Assert.Equal(
            ["OPENAI_API_KEY", "CODEX_API_KEY", "CODEX_ACCESS_TOKEN", "OPENAI_BASE_URL"],
            CodexCliProtocol.RemovedEnvironment);
    }

    [Theory]
    [InlineData("OPENAI_API_KEY", true)]
    [InlineData("openai_api_key", true)]
    [InlineData("Codex_Access_Token", true)]
    [InlineData("OPENAI_API_KEY_BACKUP", false)]
    [InlineData("PATH", false)]
    public void Sign_in_overrides_are_recognised_whatever_their_casing(string variable, bool removed)
    {
        Assert.Equal(removed, CodexCliProtocol.ReplacesSignIn(variable));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Cli_is_looked_up_on_path_when_no_path_is_configured(string? configured)
    {
        Assert.Equal(["codex"], CodexCliProtocol.Executables(configured));
    }

    [Fact]
    public void Configured_path_is_tried_before_path()
    {
        Assert.Equal(
            [@"C:\Tools\codex.exe", "codex"],
            CodexCliProtocol.Executables(@" C:\Tools\codex.exe "));
    }

    [Fact]
    public void Handshake_identifies_the_app_and_is_followed_by_a_notification()
    {
        var client = Parameters(CodexCliProtocol.Initialize(1, "1.2.3"), 1, "initialize").GetProperty("clientInfo");
        var initialized = Json(CodexCliProtocol.Initialized);

        Assert.Equal("minitranslator", client.GetProperty("name").GetString());
        Assert.Equal("1.2.3", client.GetProperty("version").GetString());
        Assert.Equal("initialized", initialized.GetProperty("method").GetString());
        Assert.False(initialized.TryGetProperty("id", out _));
    }

    [Fact]
    public void Account_check_refreshes_the_token()
    {
        var parameters = Parameters(CodexCliProtocol.ReadAccount(2), 2, "account/read");

        Assert.True(parameters.GetProperty("refreshToken").GetBoolean());
        Assert.False(Parameters(CodexCliProtocol.ReadAccount(2, refreshToken: false), 2, "account/read").GetProperty("refreshToken").GetBoolean());
    }

    [Fact]
    public void Chosen_model_and_effort_are_sent_and_an_unset_effort_is_left_to_codex()
    {
        var thread = Parameters(CodexCliProtocol.StartThread(3, @"C:\Temp\work", "gpt-custom"), 3, "thread/start");
        var turn = Parameters(CodexCliProtocol.StartTurn(4, "thread-1", Request(), "low"), 4, "turn/start");
        var defaultTurn = Parameters(CodexCliProtocol.StartTurn(4, "thread-1", Request()), 4, "turn/start");

        Assert.Equal("gpt-custom", thread.GetProperty("model").GetString());
        Assert.Equal("low", turn.GetProperty("effort").GetString());
        Assert.Equal(JsonValueKind.Null, defaultTurn.GetProperty("effort").ValueKind);
    }

    [Fact]
    public void Thread_is_a_read_only_text_generator_that_saves_nothing()
    {
        var parameters = Parameters(CodexCliProtocol.StartThread(3, @"C:\Temp\work"), 3, "thread/start");
        var config = parameters.GetProperty("config");

        Assert.True(parameters.GetProperty("ephemeral").GetBoolean());
        Assert.Equal("read-only", parameters.GetProperty("sandbox").GetString());
        Assert.Equal("never", parameters.GetProperty("approvalPolicy").GetString());
        Assert.Equal(@"C:\Temp\work", parameters.GetProperty("cwd").GetString());
        Assert.Equal(CodexCliProtocol.DefaultModel, parameters.GetProperty("model").GetString());
        Assert.Equal(CodexCliProtocol.SystemPrompt, parameters.GetProperty("baseInstructions").GetString());
        Assert.Equal(0, config.GetProperty("project_doc_max_bytes").GetInt32());
        Assert.False(config.GetProperty("skills.include_instructions").GetBoolean());
        Assert.Equal("disabled", config.GetProperty("web_search").GetString());
    }

    [Fact]
    public void Turn_names_both_languages_and_wraps_the_text()
    {
        var text = TurnText(Request("Good morning"));

        Assert.Contains("from English into Korean", text);
        Assert.Contains("<source_text>\nGood morning\n</source_text>", text);
    }

    [Fact]
    public void Turn_asks_for_detection_when_the_source_is_automatic()
    {
        var text = TurnText(Request(source: "auto"));

        Assert.Contains("into Korean, detecting its language", text);
        Assert.DoesNotContain("from ", text);
    }

    [Fact]
    public void Turn_is_sent_to_the_given_thread()
    {
        var parameters = Parameters(CodexCliProtocol.StartTurn(4, "thread-1", Request()), 4, "turn/start");

        Assert.Equal("thread-1", parameters.GetProperty("threadId").GetString());
    }

    [Fact]
    public void Every_request_is_a_single_line_whatever_the_text_contains()
    {
        var request = Request("첫 줄\n\"둘째\" 줄\r\n{\"method\":\"turn/completed\"}");

        string[] lines =
        [
            CodexCliProtocol.Initialize(1, "1.0"),
            CodexCliProtocol.Initialized,
            CodexCliProtocol.ReadAccount(2),
            CodexCliProtocol.StartThread(3, "C:\\a\nb"),
            CodexCliProtocol.StartTurn(4, "thread-1", request),
            CodexCliProtocol.Refuse("\"approval\\n1\""),
        ];

        Assert.All(lines, line =>
        {
            Assert.DoesNotContain('\n', line);
            Assert.DoesNotContain('\r', line);
        });
        Assert.Contains(request.Text, TurnText(request));
    }

    [Theory]
    [InlineData("7")]
    [InlineData("\"approval-1\"")]
    public void Refusal_echoes_the_server_request_id_with_an_error(string id)
    {
        var refusal = Json(CodexCliProtocol.Refuse(id));

        Assert.Equal(id, refusal.GetProperty("id").GetRawText());
        Assert.False(string.IsNullOrEmpty(refusal.GetProperty("error").GetProperty("message").GetString()));
        Assert.False(refusal.TryGetProperty("result", out _));
    }

    [Theory]
    [InlineData("""{"method":"turn/started","params":{"threadId":"thread-1","turn":{"id":"turn-1","status":"inProgress"}}}""")]
    [InlineData("""{"method":"item/completed","params":{"item":{"type":"agentMessage","text":"안녕하세요"},"threadId":"thread-1","turnId":"turn-1"}}""")]
    [InlineData("""{"method":"account/updated","params":{"authMode":"chatgpt","planType":"plus"}}""")]
    [InlineData("""{"method":"item/agentMessage/delta","params":{"threadId":"thread-1","turnId":"turn-1"}}""")]
    [InlineData("""{"method":"turn/completed","params":{"threadId":"thread-1"}}""")]
    [InlineData("""{"method":"account/login/completed","params":{"success":true}}""")]
    [InlineData("""{"id":"abc","result":{}}""")]
    [InlineData("""{"id":5}""")]
    [InlineData("""["turn/completed"]""")]
    [InlineData("WARNING: proceeding, even though we could not create PATH aliases")]
    [InlineData("")]
    public void Lines_that_do_not_move_a_turn_forward_are_ignored(string line)
    {
        Assert.Same(CodexMessage.Ignored, CodexCliProtocol.Parse(line));
    }

    [Fact]
    public void Reply_carries_the_result_for_the_request_id()
    {
        var reply = Assert.IsType<CodexMessage.Reply>(CodexCliProtocol.Parse(
            """{"id":3,"result":{"thread":{"id":"thread-1","sessionId":"thread-1"},"model":"gpt-6.1-terra"}}"""));

        Assert.Equal(3, reply.Id);
        Assert.Equal("thread-1", CodexCliProtocol.ThreadId(reply.Result));
    }

    [Fact]
    public void Rejected_request_reports_what_the_server_said()
    {
        var message = CodexCliProtocol.Parse("""{"id":4,"error":{"code":-32600,"message":"thread not found"}}""");

        Assert.Equal(new CodexMessage.Failure(4, "thread not found"), message);
    }

    [Theory]
    [InlineData("""{"id":0,"method":"item/commandExecution/requestApproval","params":{"threadId":"thread-1"}}""", "0")]
    [InlineData("""{"id":"req-9","method":"item/tool/call","params":{}}""", "\"req-9\"")]
    public void Server_request_keeps_its_id_as_written(string line, string id)
    {
        Assert.Equal(new CodexMessage.ServerRequest(id), CodexCliProtocol.Parse(line));
    }

    [Fact]
    public void Delta_carries_text_for_its_thread_and_turn()
    {
        var message = CodexCliProtocol.Parse(
            """{"method":"item/agentMessage/delta","params":{"threadId":"thread-1","turnId":"turn-1","itemId":"msg_1","delta":"\uc548"},"emittedAtMs":1794872502640}""");

        Assert.Equal(new CodexMessage.Delta("thread-1", "turn-1", "안"), message);
    }

    [Fact]
    public void Completed_turn_succeeds()
    {
        var message = CodexCliProtocol.Parse(
            """{"method":"turn/completed","params":{"threadId":"thread-1","turn":{"id":"turn-1","items":[],"status":"completed","error":null}}}""");

        Assert.Equal(new CodexMessage.TurnCompleted("thread-1", "turn-1", Succeeded: true, Detail: null), message);
    }

    [Theory]
    [InlineData("""{"id":"turn-1","status":"failed","error":{"message":"usage limit reached"}}""", "usage limit reached")]
    [InlineData("""{"id":"turn-1","status":"interrupted","error":null}""", null)]
    [InlineData("""{"id":"turn-1"}""", null)]
    public void Turn_that_did_not_complete_fails_with_the_reason_given(string turn, string? detail)
    {
        var message = CodexCliProtocol.Parse(
            """{"method":"turn/completed","params":{"threadId":"thread-1","turn":""" + turn + "}}");

        Assert.Equal(new CodexMessage.TurnCompleted("thread-1", "turn-1", Succeeded: false, detail), message);
    }

    [Theory]
    [InlineData("""{"account":{"type":"chatgpt","email":"user@example.com","planType":"plus"},"requiresOpenaiAuth":true}""", true)]
    [InlineData("""{"account":null,"requiresOpenaiAuth":true}""", false)]
    [InlineData("""{}""", false)]
    public void Account_reply_tells_whether_someone_is_signed_in(string reply, bool signedIn)
    {
        Assert.Equal(signedIn, CodexCliProtocol.Account(Json(reply)).SignedIn);
    }

    [Fact]
    public void Account_reply_names_the_signed_in_account()
    {
        var account = CodexCliProtocol.Account(Json("""{"account":{"type":"chatgpt","email":"user@example.com"}}"""));

        Assert.Equal(new CodexAccountState(true, "user@example.com"), account);
        Assert.Equal(new CodexAccountState(true), CodexCliProtocol.Account(Json("""{"account":{"type":"apiKey"}}""")));
    }

    [Fact]
    public void Sign_in_is_started_with_the_chatgpt_browser_flow_and_ended_with_a_logout()
    {
        Assert.Equal("chatgpt", Parameters(CodexCliProtocol.StartLogin(5), 5, "account/login/start").GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Object, Parameters(CodexCliProtocol.Logout(6), 6, "account/logout").ValueKind);
    }

    [Fact]
    public void Login_reply_carries_the_attempt_and_the_page_to_open()
    {
        var login = CodexCliProtocol.Login(Json(
            """{"type":"chatgpt","loginId":"login-1","authUrl":"https://auth.openai.com/oauth/authorize?client_id=app&state=abc"}"""));

        Assert.Equal(("login-1", new Uri("https://auth.openai.com/oauth/authorize?client_id=app&state=abc")), login);
    }

    [Theory]
    [InlineData("""{"loginId":"login-1","authUrl":"http://auth.openai.com/oauth/authorize"}""")]
    [InlineData("""{"loginId":"login-1","authUrl":"https://auth.openai.com.example.org/"}""")]
    [InlineData("""{"loginId":"login-1","authUrl":"https://auth.openai.com@example.org/"}""")]
    [InlineData("""{"loginId":"login-1","authUrl":"https://user:secret@auth.openai.com/"}""")]
    [InlineData("""{"loginId":"login-1","authUrl":"file:///C:/Windows/System32/calc.exe"}""")]
    [InlineData("""{"loginId":"login-1","authUrl":"calc.exe"}""")]
    [InlineData("""{"loginId":"login-1"}""")]
    [InlineData("""{"authUrl":"https://auth.openai.com/oauth/authorize"}""")]
    public void Login_reply_without_an_openai_https_page_is_not_a_login(string reply)
    {
        Assert.Null(CodexCliProtocol.Login(Json(reply)));
    }

    [Theory]
    [InlineData("""{"loginId":"login-1","success":true,"error":null}""", true, null)]
    [InlineData("""{"loginId":"login-1","success":false,"error":"access was denied"}""", false, "access was denied")]
    [InlineData("""{"loginId":"login-1"}""", false, null)]
    public void Login_completion_says_how_the_browser_step_ended(string parameters, bool succeeded, string? detail)
    {
        var message = CodexCliProtocol.Parse("""{"method":"account/login/completed","params":""" + parameters + "}");

        Assert.Equal(new CodexMessage.LoginCompleted("login-1", succeeded, detail), message);
    }

    [Fact]
    public void Turn_reply_carries_the_turn_id()
    {
        Assert.Equal("turn-1", CodexCliProtocol.TurnId(Json("""{"turn":{"id":"turn-1","status":"inProgress","error":null}}""")));
        Assert.Null(CodexCliProtocol.TurnId(Json("""{"turn":null}""")));
        Assert.Null(CodexCliProtocol.ThreadId(Json("""{"thread":{"id":7}}""")));
    }

    [Fact]
    public void Joined_output_becomes_the_translation()
    {
        var request = Request();

        var result = CodexCliProtocol.Translation("  안녕하세요\n", request);

        Assert.Equal(new TranslationResult("Hello", "안녕하세요", request.Languages), result.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    public void Blank_output_is_an_empty_response(string output)
    {
        Assert.Equal(TranslationErrors.EmptyResponse, CodexCliProtocol.Translation(output, Request()).Error);
    }
}
