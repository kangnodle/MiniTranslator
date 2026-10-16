using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.GoogleTranslate.Tests;

public class GoogleTranslateProtocolTests
{
    private static TranslationRequest Request(string source = "en", string text = "Hello") =>
        TranslationRequest.Create(text, LanguagePair.Create(source, "ko").Value).Value;

    [Fact]
    public void Request_uri_contains_client_source_and_target_but_no_key()
    {
        var uri = GoogleTranslateProtocol.BuildRequestUri(Request());

        Assert.StartsWith(GoogleTranslateProtocol.Endpoint + "?", uri);
        Assert.Contains("client=gtx", uri);
        Assert.Contains("sl=en", uri);
        Assert.Contains("tl=ko", uri);
        Assert.DoesNotContain("key=", uri);
    }

    [Fact]
    public void Request_uri_uses_auto_for_auto_detect()
    {
        Assert.Contains("sl=auto", GoogleTranslateProtocol.BuildRequestUri(Request("auto")));
    }

    [Fact]
    public void Request_uri_encodes_the_text_as_a_single_query_parameter()
    {
        const string text = "a & b = 안녕 + # ? /\n😀";
        var uri = GoogleTranslateProtocol.BuildRequestUri(Request(text: text));

        Assert.EndsWith("&q=" + Uri.EscapeDataString(text), uri);
    }

    [Fact]
    public async Task Translator_sends_a_get_with_text_in_the_query_and_no_body()
    {
        using var handler = new GetOnlyHandler();
        using var http = new HttpClient(handler);

        var result = await new GoogleTranslator(http).TranslateAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.Equal("안녕하세요", result.Value.TranslatedText);
    }

    private sealed class GetOnlyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(GoogleTranslateProtocol.BuildRequestUri(Request()), request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Content);
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""[[["안녕하세요","Hello"]],null,"en"]"""),
            });
        }
    }

    [Fact]
    public void Parses_and_joins_all_sentences()
    {
        const string body = """[[["안녕하세요. ","Hello. ",null,null,3],["잘 지내세요?","How are you?",null,null,10]],null,"en",null,null,null,0.98,[]]""";

        var result = GoogleTranslateProtocol.ParseResponse(200, body, Request("auto")).Value;

        Assert.Equal("안녕하세요. 잘 지내세요?", result.TranslatedText);
        Assert.Equal("en", result.DetectedSourceLanguageCode);
        Assert.Equal("Hello", result.SourceText);
    }

    [Fact]
    public void Http_errors_report_the_status()
    {
        var error = GoogleTranslateProtocol.ParseResponse(503, "<html>Service Unavailable</html>", Request()).Error;

        Assert.Equal("Translation.ProviderRejected", error.Code);
        Assert.Contains("HTTP 503", error.Message);
    }

    [Fact]
    public void Rate_limit_reports_an_actionable_error_without_parsing_the_block_page()
    {
        var error = GoogleTranslateProtocol.ParseResponse(429, "<html>Too Many Requests</html>", Request()).Error;

        Assert.Equal(TranslationErrors.RateLimited, error);
        Assert.Contains("HTTP 429", error.Message);
        Assert.Contains("잠시 후", error.Message);
    }

    [Theory]
    [InlineData("""[[],null,"en"]""")]
    [InlineData("""[null]""")]
    [InlineData("""{}""")]
    [InlineData("not json")]
    public void Empty_or_malformed_success_is_an_empty_response(string body)
    {
        Assert.Equal(TranslationErrors.EmptyResponse, GoogleTranslateProtocol.ParseResponse(200, body, Request()).Error);
    }
}
