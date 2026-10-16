using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Languages;

/// <summary>The fixed set of languages offered in the UI.</summary>
public static class LanguageCatalog
{
    public static Language AutoDetect { get; } = new(Language.AutoDetectCode, "Detect language", "자동 감지");

    public static Language English { get; } = new("en", "English", "English");

    public static Language Korean { get; } = new("ko", "Korean", "한국어");

    /// <summary>All concrete languages (usable as source or target).</summary>
    public static IReadOnlyList<Language> All { get; } =
    [
        Korean,
        English,
        new("ja", "Japanese", "日本語"),
        new("zh-CN", "Chinese (Simplified)", "简体中文"),
        new("zh-TW", "Chinese (Traditional)", "繁體中文"),
        new("es", "Spanish", "Español"),
        new("fr", "French", "Français"),
        new("de", "German", "Deutsch"),
        new("it", "Italian", "Italiano"),
        new("pt", "Portuguese", "Português"),
        new("ru", "Russian", "Русский"),
        new("vi", "Vietnamese", "Tiếng Việt"),
        new("th", "Thai", "ไทย"),
        new("id", "Indonesian", "Bahasa Indonesia"),
        new("ms", "Malay", "Bahasa Melayu"),
        new("tr", "Turkish", "Türkçe"),
        new("ar", "Arabic", "العربية"),
        new("hi", "Hindi", "हिन्दी"),
        new("nl", "Dutch", "Nederlands"),
        new("pl", "Polish", "Polski"),
        new("uk", "Ukrainian", "Українська"),
        new("sv", "Swedish", "Svenska"),
    ];

    /// <summary>Languages selectable as the source: auto-detect plus every concrete language.</summary>
    public static IReadOnlyList<Language> SourceOptions { get; } = [AutoDetect, .. All];

    /// <summary>Languages selectable as the target (auto-detect is not a valid target).</summary>
    public static IReadOnlyList<Language> TargetOptions => All;

    public static Result<Language> FromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return LanguageErrors.Unknown(code);
        }

        var match = SourceOptions.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));
        return match is null ? LanguageErrors.Unknown(code) : match;
    }

    /// <summary>Finds a catalog language by code, or <c>null</c> when the code is unknown.</summary>
    public static Language? TryFind(string? code) => FromCode(code).Match<Language?>(l => l, _ => null);

    /// <summary>Looks up a language name for a provider-reported code, falling back to the raw code.</summary>
    public static string DescribeCode(string code) => TryFind(code)?.DisplayName ?? code;
}
