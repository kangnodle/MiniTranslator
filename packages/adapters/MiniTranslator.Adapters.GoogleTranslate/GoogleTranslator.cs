using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.GoogleTranslate;

internal sealed class GoogleTranslator(HttpClient http) : ITranslator
{
    public async Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, GoogleTranslateProtocol.BuildRequestUri(request));

        try
        {
            using var response = await http.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return GoogleTranslateProtocol.ParseResponse((int)response.StatusCode, body, request);
        }
        catch (HttpRequestException ex)
        {
            return TranslationErrors.Network(ex.Message);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TranslationErrors.Network("요청 시간이 초과되었습니다.");
        }
    }
}
