using MiniTranslator.Domain.Languages;

namespace MiniTranslator.Domain.Translations;

public sealed record TranslationResult(
    string SourceText,
    string TranslatedText,
    LanguagePair Languages,
    string? DetectedSourceLanguageCode = null)
{
    public Language? DetectedSourceLanguage => LanguageCatalog.TryFind(DetectedSourceLanguageCode);

    public string? DetectedSourceLanguageName =>
        DetectedSourceLanguageCode is null ? null : LanguageCatalog.DescribeCode(DetectedSourceLanguageCode);

    public string? DetectionNote =>
        Languages.Source.IsAutoDetect && DetectedSourceLanguageName is { } name ? $"감지된 언어: {name}" : null;
}
