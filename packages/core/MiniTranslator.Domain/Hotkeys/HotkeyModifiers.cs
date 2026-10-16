namespace MiniTranslator.Domain.Hotkeys;

/// <summary>
/// Modifier keys of a global hotkey.
/// Values intentionally match the Win32 <c>MOD_*</c> flags so adapters can pass them through unchanged.
/// </summary>
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
    /// <summary>Display names of the set flags in canonical order, e.g. <c>["Ctrl", "Shift"]</c>.</summary>
    public static IEnumerable<string> Names(this HotkeyModifiers modifiers)
    {
        if (modifiers.HasFlag(HotkeyModifiers.Control)) yield return "Ctrl";
        if (modifiers.HasFlag(HotkeyModifiers.Alt)) yield return "Alt";
        if (modifiers.HasFlag(HotkeyModifiers.Shift)) yield return "Shift";
        if (modifiers.HasFlag(HotkeyModifiers.Windows)) yield return "Win";
    }

    /// <summary>
    /// Text shown while modifiers are held but the main key is not pressed yet, e.g. <c>"Ctrl + Alt + …"</c>.
    /// </summary>
    public static string ToPendingDisplayString(this HotkeyModifiers modifiers) =>
        string.Join(" + ", [.. modifiers.Names(), "…"]);
}
