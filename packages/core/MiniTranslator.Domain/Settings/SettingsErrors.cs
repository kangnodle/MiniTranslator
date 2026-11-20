using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Settings;

public static class SettingsErrors
{
    public static readonly Error InvalidUiLanguage = new("Settings.InvalidUiLanguage", "한국어 또는 영어를 선택하세요.");
    public static readonly Error InvalidFont = new("Settings.InvalidFont", "앱 글꼴을 다시 선택하세요.");
    public static readonly Error InvalidEngine = new("Settings.InvalidEngine", "번역 엔진을 다시 선택하세요.");
    public static readonly Error InvalidTheme = new("Settings.InvalidTheme", "테마를 다시 선택하세요.");
}
