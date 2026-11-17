namespace MiniTranslator.Domain.Hotkeys;

public static class VirtualKeys
{
    public const int Space = 0x20;
    public const int Escape = 0x1B;

    private static readonly IReadOnlyDictionary<int, string> NamesByCode = BuildNames();

    private static readonly IReadOnlyDictionary<string, int> CodesByName =
        NamesByCode.ToDictionary(kv => kv.Value, kv => kv.Key, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupported(int virtualKey) => NamesByCode.ContainsKey(virtualKey);

    public static bool IsModifier(int virtualKey) =>
        virtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C or (>= 0xA0 and <= 0xA5);

    public static string NameOf(int virtualKey) =>
        NamesByCode.TryGetValue(virtualKey, out var name) ? name : $"0x{virtualKey:X2}";

    public static int? FromName(string? name) =>
        name is not null && CodesByName.TryGetValue(name.Trim(), out var code) ? code : null;

    private static Dictionary<int, string> BuildNames()
    {
        var names = new Dictionary<int, string>
        {
            [Space] = "Space",
            [0x0D] = "Enter",
            [0x09] = "Tab",
            [0x08] = "Backspace",
            [0x2D] = "Insert",
            [0x2E] = "Delete",
            [0x24] = "Home",
            [0x23] = "End",
            [0x21] = "PageUp",
            [0x22] = "PageDown",
            [0x25] = "Left",
            [0x26] = "Up",
            [0x27] = "Right",
            [0x28] = "Down",
            [0xC0] = "`",
            [0xBD] = "-",
            [0xBB] = "=",
            [0xDB] = "[",
            [0xDD] = "]",
            [0xDC] = "\\",
            [0xBA] = ";",
            [0xDE] = "'",
            [0xBC] = ",",
            [0xBE] = ".",
            [0xBF] = "/",
        };

        for (var c = '0'; c <= '9'; c++) names[c] = c.ToString();
        for (var c = 'A'; c <= 'Z'; c++) names[c] = c.ToString();
        for (var i = 0; i <= 9; i++) names[0x60 + i] = $"Num{i}";
        for (var i = 1; i <= 24; i++) names[0x70 + i - 1] = $"F{i}";

        return names;
    }
}
