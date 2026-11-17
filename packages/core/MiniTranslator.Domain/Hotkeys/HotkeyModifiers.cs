namespace MiniTranslator.Domain.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 0x1,
    Control = 0x2,
    Shift = 0x4,
    Windows = 0x8,
}

public static class HotkeyModifiersExtensions
{
    public static IEnumerable<string> Names(this HotkeyModifiers modifiers)
    {
        if (modifiers.HasFlag(HotkeyModifiers.Control)) yield return "Ctrl";
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) yield return "Alt";
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) yield return "Shift";
        if (modifiers.HasFlag(HotkeyModifiers.Windows)) yield return "Win";
    }

    public static string ToPendingDisplayString(this HotkeyModifiers modifiers) =>
        string.Join(" + ", [.. modifiers.Names(), "…"]);
}
