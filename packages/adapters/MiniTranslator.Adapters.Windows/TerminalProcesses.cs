namespace MiniTranslator.Adapters.Windows;

/// <summary>
/// Identifies terminal and console processes where sending Ctrl+C via SendInput
/// would trigger SIGINT / Ctrl+C interrupts instead of copying text.
/// </summary>
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
