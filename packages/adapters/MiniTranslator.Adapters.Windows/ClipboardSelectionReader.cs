using System.Runtime.InteropServices;
using MiniTranslator.Application.Abstractions;

namespace MiniTranslator.Adapters.Windows;

/// <summary>
/// Reads the foreground app's selection by sending Ctrl+C and watching the clipboard,
/// then restores the user's previous clipboard text. Must run on the UI (STA) thread.
/// Which keys to wait for and inject comes from <see cref="CopyShortcut"/>; this class only performs the I/O.
/// </summary>
internal sealed class ClipboardSelectionReader(WpfClipboard clipboard) : ITextSelectionReader
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);
    private const int ModifierReleasePolls = 50; // ~500 ms
    private const int ClipboardPolls = 20;       // ~200 ms

    public async Task<string?> ReadSelectionAsync(CancellationToken cancellationToken = default)
    {
        var sourceWindow = NativeMethods.GetForegroundWindow();
        NativeMethods.GetWindowThreadProcessId(sourceWindow, out var sourceProcessId);
        if (sourceProcessId == 0 || IsTerminalForegroundProcess(sourceProcessId))
        {
            return null;
        }

        // The user may still be holding the hotkey's modifiers; Ctrl+C would become e.g. Ctrl+Alt+C.
        for (var i = 0; i < ModifierReleasePolls && AnyConflictingModifierDown(); i++)
        {
            await Task.Delay(PollInterval, cancellationToken);
        }

        var sequenceBefore = NativeMethods.GetClipboardSequenceNumber();
        var backup = clipboard.CaptureBackup();

        if (NativeMethods.GetForegroundWindow() != sourceWindow
            || NativeMethods.GetClipboardSequenceNumber() != sequenceBefore
            || !SendCopyShortcut())
        {
            return null;
        }

        for (var i = 0; i < ClipboardPolls; i++)
        {
            await Task.Delay(PollInterval, cancellationToken);
            var sequence = NativeMethods.GetClipboardSequenceNumber();
            if (sequence != sequenceBefore)
            {
                await Task.Delay(PollInterval, cancellationToken); // let the source app finish writing all formats
                return clipboard.TryReadSelection(sourceWindow, sourceProcessId, sequence, backup);
            }
        }

        return null; // Clipboard unchanged: nothing was selected.
    }

    private static bool IsTerminalForegroundProcess(uint processId)
    {
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            return TerminalProcesses.IsTerminalProcess(process.ProcessName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool AnyConflictingModifierDown() => CopyShortcut.ConflictingModifierKeys.Any(IsKeyDown);

    private static bool IsKeyDown(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    private static bool SendCopyShortcut()
    {
        var conflictingModifierHeld = AnyConflictingModifierDown();
        var strokes = CopyShortcut.Strokes(
            controlHeld: !conflictingModifierHeld && IsKeyDown(CopyShortcut.ControlKey),
            conflictingModifierHeld);
        if (strokes.Count == 0)
        {
            return false;
        }

        var inputs = strokes.Select(NativeMethods.KeyInput).ToArray();
        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        return sent == inputs.Length;
    }
}
