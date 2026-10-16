namespace MiniTranslator.Adapters.Windows;

/// <summary>One injected keyboard event.</summary>
internal readonly record struct KeyStroke(ushort VirtualKey, bool KeyUp);

/// <summary>
/// Pure policy for capturing the foreground selection with Ctrl+C: which held keys would spoil the shortcut,
/// and which key events to inject. <see cref="ClipboardSelectionReader"/> performs the actual I/O.
/// </summary>
internal static class CopyShortcut
{
    public const ushort ControlKey = 0x11;
    public const ushort CKey = 0x43;

    /// <summary>
    /// Shift, Alt, LWin, RWin. Ctrl is deliberately absent: a held Ctrl already forms Ctrl+C,
    /// so there is no need to wait for its release.
    /// </summary>
    public static IReadOnlyList<int> ConflictingModifierKeys { get; } = [0x10, 0x12, 0x5B, 0x5C];

    /// <summary>
    /// The events that type Ctrl+C. A physically held Ctrl is reused: injecting a Ctrl key-up
    /// would desync it from the key the user still holds. A conflicting modifier yields no events.
    /// </summary>
    public static IReadOnlyList<KeyStroke> Strokes(bool controlHeld, bool conflictingModifierHeld) => conflictingModifierHeld
        ? []
        : controlHeld
        ?
        [
            new(CKey, KeyUp: false),
            new(CKey, KeyUp: true),
        ]
        :
        [
            new(ControlKey, KeyUp: false),
            new(CKey, KeyUp: false),
            new(CKey, KeyUp: true),
            new(ControlKey, KeyUp: true),
        ];
}
