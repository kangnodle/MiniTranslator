using MiniTranslator.Domain.Hotkeys;

namespace MiniTranslator.Adapters.Windows;

internal static class KeyboardLayoutAliases
{
    public const int Hanja = 0x19;

    public static int? For(HotkeyGesture gesture) =>
        gesture.Modifiers == HotkeyModifiers.Control && gesture.VirtualKey == VirtualKeys.Space
            ? Hanja
            : null;
}
