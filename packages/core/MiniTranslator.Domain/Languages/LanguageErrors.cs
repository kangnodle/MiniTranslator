using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Languages;

public static class LanguageErrors
{
    public static Error Unknown(string? code) =>
        new("Language.Unknown", $"지원하지 않는 언어 코드입니다: '{code}'") { Parameter = code };

    public static Error TargetCannotBeAutoDetect { get; } =
        new("Language.TargetAutoDetect", "도착 언어는 '자동 감지'로 설정할 수 없습니다.");

    public static Error SameSourceAndTarget { get; } =
        new("Language.SameLanguage", "출발 언어와 도착 언어가 같습니다.");

    public static Error CannotSwapAutoDetect { get; } =
        new("Language.CannotSwapAutoDetect", "출발 언어가 '자동 감지'일 때는 언어를 서로 바꿀 수 없습니다.");
}
