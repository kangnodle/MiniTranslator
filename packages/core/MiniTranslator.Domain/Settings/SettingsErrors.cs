using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Settings;

public static class SettingsErrors
{
    public static readonly Error InvalidUiLanguage = new("Settings.InvalidUiLanguage", "한국어 또는 영어를 선택하세요.");
    public static readonly Error InvalidFont = new("Settings.InvalidFont", "앱 글꼴을 다시 선택하세요.");
    public static readonly Error InvalidEngine = new("Settings.InvalidEngine", "번역 엔진을 다시 선택하세요.");
    public static readonly Error InvalidTheme = new("Settings.InvalidTheme", "테마를 다시 선택하세요.");
    public static readonly Error InvalidCodexExecutablePath = new("Settings.InvalidCodexExecutablePath", "로컬 드라이브의 .exe 파일 전체 경로를 입력하거나 비워 두세요.");
    public static readonly Error InvalidModel = new("Settings.InvalidModel", "모델 이름에 사용할 수 없는 문자가 포함되어 있습니다.");
    public static readonly Error InvalidEffort = new("Settings.InvalidEffort", "추론 강도 값에 사용할 수 없는 문자가 포함되어 있습니다.");
}
