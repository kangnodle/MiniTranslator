using MiniTranslator.Domain.Hotkeys;

namespace MiniTranslator.Adapters.Windows;

/// <summary>Pure lookup of keys that a keyboard layout reports instead of the one the user pressed.</summary>
internal static class KeyboardLayoutAliases
{
    /// <summary>Hanja key; the Korean layout reports Ctrl+Space as this key.</summary>
    public const int Hanja = 0x19;

    /// <summary>
    /// The Korean keyboard layout turns Ctrl+Space into the Hanja key (<c>VK_HANJA</c> with Ctrl held),
    /// so a plain Ctrl+Space registration never fires there; registering the alias too catches that translated key.
    /// </summary>
    public static int? For(HotkeyGesture gesture) =>
        gesture.Modifiers == HotkeyModifiers.Control && gesture.VirtualKey == VirtualKeys.Space
            ? Hanja
            : null;
}
