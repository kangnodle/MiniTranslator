using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Hotkeys;

public static class HotkeyErrors
{
    public static Error ModifierRequired { get; } =
        new("Hotkey.ModifierRequired", "단축키에는 Ctrl, Alt, Win 중 하나 이상의 조합 키가 필요합니다.");

    public static Error ShiftOnly { get; } =
        new("Hotkey.ShiftOnly", "Shift만으로는 단축키를 만들 수 없습니다. Ctrl, Alt 또는 Win을 함께 사용하세요.");

    public static Error KeyRequired { get; } =
        new("Hotkey.KeyRequired", "조합 키와 함께 누를 일반 키를 지정하세요.");

    public static Error UnsupportedKey(int virtualKey) =>
        new("Hotkey.UnsupportedKey", $"단축키로 사용할 수 없는 키입니다. ({VirtualKeys.NameOf(virtualKey)})") { Parameter = VirtualKeys.NameOf(virtualKey) };

    public static Error Unparsable(string? text) =>
        new("Hotkey.Unparsable", $"단축키 형식을 해석할 수 없습니다: '{text}'") { Parameter = text };

    public static Error AlreadyInUse(HotkeyGesture gesture) =>
        new("Hotkey.AlreadyInUse", $"'{gesture.ToDisplayString()}' 단축키를 등록할 수 없습니다. 다른 프로그램이 사용 중일 수 있습니다.") { Parameter = gesture.ToDisplayString() };
}
