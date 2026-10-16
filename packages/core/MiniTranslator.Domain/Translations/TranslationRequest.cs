using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Languages;

namespace MiniTranslator.Domain.Translations;

/// <summary>An always-valid request to translate a piece of text.</summary>
public sealed record TranslationRequest
{
    /// <summary>Upper bound recommended by Google for a single request.</summary>
    public const int MaxLength = 5000;

    private TranslationRequest(string text, LanguagePair languages)
    {
        Text = text;
        Languages = languages;
    }

    public string Text { get; }

    public LanguagePair Languages { get; }

    public static Result<TranslationRequest> Create(string? text, LanguagePair languages)
    {
        var normalized = text?.Trim() ?? string.Empty;

        if (normalized.Length == 0)
        {
            return TranslationErrors.EmptyText;
        }

        if (normalized.Length > MaxLength)
        {
            return TranslationErrors.TooLong(normalized.Length);
        }

        return new TranslationRequest(normalized, languages);
    }
}
