using MiniTranslator.Domain.Languages;

namespace MiniTranslator.Domain.Translations;

/// <param name="SourceText">The text that was translated.</param>
/// <param name="TranslatedText">The translation.</param>
/// <param name="Languages">The language pair that was requested.</param>
/// <param name="DetectedSourceLanguageCode">Provider-reported source language when auto-detect was used.</param>
public sealed record TranslationResult(
    string SourceText,
    string TranslatedText,
    LanguagePair Languages,
    string? DetectedSourceLanguageCode = null)
{
    /// <summary>The detected source language when it is one the catalog knows, otherwise <c>null</c>.</summary>
    public Language? DetectedSourceLanguage => LanguageCatalog.TryFind(DetectedSourceLanguageCode);

    public string? DetectedSourceLanguageName =>
        DetectedSourceLanguageCode is null ? null : LanguageCatalog.DescribeCode(DetectedSourceLanguageCode);

    /// <summary>Note shown to the user when the source was auto-detected, e.g. <c>"감지된 언어: English"</c>.</summary>
    public string? DetectionNote =>
        Languages.Source.IsAutoDetect && DetectedSourceLanguageName is { } name ? $"감지된 언어: {name}" : null;
}
