using System.Text.Json;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Adapters.GoogleTranslate;

internal static class GoogleTranslateProtocol
{
    public const string Endpoint = "https://translate.googleapis.com/translate_a/single";

    public static string BuildRequestUri(TranslationRequest request) =>
        $"{Endpoint}?client=gtx&dt=t" +
        $"&sl={Uri.EscapeDataString(request.Languages.Source.Code)}" +
        $"&tl={Uri.EscapeDataString(request.Languages.Target.Code)}" +
        $"&q={Uri.EscapeDataString(request.Text)}";

    public static Result<TranslationResult> ParseResponse(int statusCode, string body, TranslationRequest request)
    {
        if (statusCode == 429)
        {
            return TranslationErrors.RateLimited;
        }

        if (statusCode is < 200 or >= 300)
        {
            return TranslationErrors.ProviderRejected(statusCode, null);
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Array
                || root.GetArrayLength() == 0
                || root[0].ValueKind != JsonValueKind.Array)
            {
                return TranslationErrors.EmptyResponse;
            }

            var translated = string.Concat(
                root[0].EnumerateArray()
                    .Where(sentence => sentence.ValueKind == JsonValueKind.Array && sentence.GetArrayLength() > 0)
                    .Select(sentence => sentence[0])
                    .Where(text => text.ValueKind == JsonValueKind.String)
                    .Select(text => text.GetString()));

            var detected = root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String
                ? root[2].GetString()
                : null;

            return translated.Length == 0
                ? TranslationErrors.EmptyResponse
                : new TranslationResult(request.Text, translated, request.Languages, detected);
        }
        catch (JsonException)
        {
            return TranslationErrors.EmptyResponse;
        }
    }
}
