using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Hotkeys;

public sealed record HotkeyGesture
{
    private const HotkeyModifiers AllModifiers =
        HotkeyModifiers.Alt | HotkeyModifiers.Control | HotkeyModifiers.Shift | HotkeyModifiers.Windows;

    private HotkeyGesture(HotkeyModifiers modifiers, int virtualKey)
    {
        Modifiers = modifiers;
        VirtualKey = virtualKey;
    }

    public HotkeyModifiers Modifiers { get; }

    public int VirtualKey { get; }

    public static HotkeyGesture Default { get; } = new(HotkeyModifiers.Control, VirtualKeys.Space);

    public static Result<HotkeyGesture> Create(HotkeyModifiers modifiers, int virtualKey)
    {
        modifiers &= AllModifiers;

        if (VirtualKeys.IsModifier(virtualKey) || virtualKey <= 0)
        {
            return HotkeyErrors.KeyRequired;
        }

        if (modifiers == HotkeyModifiers.None)
        {
            return HotkeyErrors.ModifierRequired;
        }

        if (modifiers == HotkeyModifiers.Shift)
        {
            return HotkeyErrors.ShiftOnly;
        }

        if (!VirtualKeys.IsSupported(virtualKey))
        {
            return HotkeyErrors.UnsupportedKey(virtualKey);
        }

        return new HotkeyGesture(modifiers, virtualKey);
    }

    public static Result<HotkeyGesture> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return HotkeyErrors.Unparsable(text);
        }

        var tokens = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return HotkeyErrors.Unparsable(text);
        }

        var modifiers = HotkeyModifiers.None;
        foreach (var token in tokens[..^1])
        {
            var modifier = ParseModifier(token);
            if (modifier is null)
            {
                return HotkeyErrors.Unparsable(text);
            }

            modifiers |= modifier.Value;
        }

        var key = VirtualKeys.FromName(tokens[^1]);
        return key is null ? HotkeyErrors.Unparsable(text) : Create(modifiers, key.Value);
    }

    public string ToDisplayString() => string.Join(" + ", Parts());

    public override string ToString() => string.Join("+", Parts());

    private IEnumerable<string> Parts() => [.. Modifiers.Names(), VirtualKeys.NameOf(VirtualKey)];

    private static HotkeyModifiers? ParseModifier(string token) => token.ToUpperInvariant() switch
    {
        "CTRL" or "CONTROL" => HotkeyModifiers.Control,
        "ALT" => HotkeyModifiers.Alt,
        "SHIFT" => HotkeyModifiers.Shift,
        "WIN" or "WINDOWS" => HotkeyModifiers.Windows,
        _ => null,
    };
}
