using System.Buffers;
using System.IO;
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
        var backup = await clipboard.CaptureBackupAsync(cancellationToken);

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
                return await clipboard.TryReadSelectionAsync(sourceWindow, sourceProcessId, sequence, backup, cancellationToken);
            }
        }

        return null;
    }

    internal static unsafe bool IsTerminalForegroundProcess(uint processId)
    {
        var handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, bInheritHandle: false, processId);
        if (handle == 0)
        {
            return false;
        }

        try
        {
            var capacity = 1024;
            while (capacity <= 32768)
            {
                var buffer = ArrayPool<char>.Shared.Rent(capacity);
                try
                {
                    var length = (uint)buffer.Length;
                    fixed (char* pointer = buffer)
                    {
                        if (NativeMethods.QueryFullProcessImageNameW(handle, 0, pointer, ref length))
                        {
                            if (length == 0)
                            {
                                return false;
                            }

                            var name = Path.GetFileNameWithoutExtension(buffer.AsSpan(0, (int)length));
                            return TerminalProcesses.IsTerminalProcess(name);
                        }
                    }

                    if (Marshal.GetLastPInvokeError() != NativeMethods.ERROR_INSUFFICIENT_BUFFER)
                    {
                        return false;
                    }
                }
                finally
                {
                    ArrayPool<char>.Shared.Return(buffer);
                }

                capacity *= 2;
            }

            return false;
        }
        finally
        {
            _ = NativeMethods.CloseHandle(handle);
        }
    }

    private static bool AnyConflictingModifierDown()
    {
        var keys = CopyShortcut.ConflictingModifierKeys;
        for (var i = 0; i < keys.Count; i++)
        {
            if (IsKeyDown(keys[i]))
            {
                return true;
            }
        }

        return false;
    }

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

        var inputs = new NativeMethods.INPUT[strokes.Count];
        for (var i = 0; i < strokes.Count; i++)
        {
            inputs[i] = NativeMethods.KeyInput(strokes[i]);
        }

        var sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
        return sent == inputs.Length;
    }
}
