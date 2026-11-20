namespace MiniTranslator.Adapters.Windows;

internal static class TerminalProcesses
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "cmd",
        "powershell",
        "powershell_ise",
        "pwsh",
        "windowsterminal",
        "conhost",
        "conemu",
        "conemu64",
        "conemuc",
        "cmder",
        "wsl",
        "bash",
        "git-bash",
        "mintty",
        "alacritty",
        "wezterm",
        "wezterm-gui",
        "putty",
        "warp",
        "ghostty",
        "kitty",
        "rio",
        "hyper",
        "tabby",
        "terminus",
        "wt",
    };

    private static readonly HashSet<string>.AlternateLookup<ReadOnlySpan<char>> SpanLookup = Names.GetAlternateLookup<ReadOnlySpan<char>>();

    public static bool IsTerminalProcess(string? processName) =>
        !string.IsNullOrWhiteSpace(processName) && Names.Contains(processName);

    public static bool IsTerminalProcess(ReadOnlySpan<char> processName) =>
        !processName.IsEmpty && !processName.IsWhiteSpace() && SpanLookup.Contains(processName);
}
