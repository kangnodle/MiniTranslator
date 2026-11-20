namespace MiniTranslator.Adapters.Windows;

internal static class TerminalProcesses
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd",
        "powershell",
        "pwsh",
        "windowsterminal",
        "conhost",
        "wsl",
        "bash",
        "mintty",
        "alacritty",
        "wezterm",
        "hyper",
        "tabby",
        "wt",
    };

    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SpanLookup = Names.GetAlternateLookup<ReadOnlySpan<char>>();

    public static bool IsTerminalProcess(string? processName) =>
        !string.IsNullOrWhiteSpace(processName) && Names.Contains(processName);

    public static bool IsTerminalProcess(ReadOnlySpan<char> processName) =>
        !processName.IsEmpty && !processName.IsWhiteSpace() && SpanLookup.Contains(processName);
}
