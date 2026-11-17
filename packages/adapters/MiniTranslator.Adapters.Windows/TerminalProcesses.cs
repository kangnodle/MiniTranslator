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

    public static bool IsTerminalProcess(string? processName) =>
        !string.IsNullOrWhiteSpace(processName) && Names.Contains(processName);
}
