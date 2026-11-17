using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Languages;

public static class LanguageCatalog
{
    public static Language AutoDetect { get; } = new(Language.AutoDetectCode, "Detect language", "자동 감지");

    public static Language English { get; } = new("en", "English", "English");

    public static Language Korean { get; } = new("ko", "Korean", "한국어");

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

    public static IReadOnlyList<Language> SourceOptions { get; } = [AutoDetect, .. All];

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

    public static Language? TryFind(string? code) => FromCode(code).Match<Language?>(l => l, _ => null);

    public static string DescribeCode(string code) => TryFind(code)?.DisplayName ?? code;
}
