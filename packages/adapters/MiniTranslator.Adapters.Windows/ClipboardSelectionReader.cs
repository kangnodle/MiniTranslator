using System.Runtime.InteropServices;
using MiniTranslator.Application.Abstractions;

namespace MiniTranslator.Adapters.Windows;

internal sealed class ClipboardSelectionReader(WpfClipboard clipboard) : ITextSelectionReader
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);
    private const int ModifierReleasePolls = 50;
    private const int ClipboardPolls = 20;

    public async Task<string?> ReadSelectionAsync(CancellationToken cancellationToken = default)
    {
        var sourceWindow = NativeMethods.GetForegroundWindow();
        NativeMethods.GetWindowThreadProcessId(sourceWindow, out var sourceProcessId);
        if (sourceProcessId == 0 || IsTerminalForegroundProcess(sourceProcessId))
        {
            return null;
        }

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
                await Task.Delay(PollInterval, cancellationToken);
                return clipboard.TryReadSelection(sourceWindow, sourceProcessId, sequence, backup);
            }
        }

        return null;
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
