namespace MiniTranslator.Adapters.Windows;

internal readonly record struct KeyStroke(ushort VirtualKey, bool KeyUp);

internal static class CopyShortcut
{
    public const ushort ControlKey = 0x11;
    public const ushort CKey = 0x43;

    public static IReadOnlyList<int> ConflictingModifierKeys { get; } = [0x10, 0x12, 0x5B, 0x5C];

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
