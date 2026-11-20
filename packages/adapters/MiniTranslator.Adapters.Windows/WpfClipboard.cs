using System.Runtime.InteropServices;
using System.Windows.Interop;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;

namespace MiniTranslator.Adapters.Windows;

internal sealed class WpfClipboard : IClipboard, IDisposable
{
    private const uint UnicodeText = 13;
    private const int Attempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(20);
    private HwndSource? _window;

    public Result<Unit> SetText(string text)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(text, copy: true);
                return Result.Success();
            }
            catch (COMException) when (attempt < Attempts)
            {
                Thread.Sleep(RetryDelay);
            }
            catch (COMException ex)
            {
                return ClipboardErrors.Busy(ex.Message);
            }
        }
    }

    public async Task<ClipboardBackup> CaptureBackupAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    return new ClipboardBackup(System.Windows.Clipboard.GetText(), null, false);
                }

                if (System.Windows.Clipboard.ContainsImage())
                {
                    return new ClipboardBackup(null, System.Windows.Clipboard.GetImage(), false);
                }

                if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    return new ClipboardBackup(null, System.Windows.Clipboard.GetFileDropList(), false);
                }

                var dataObject = System.Windows.Clipboard.GetDataObject();
                var formats = dataObject?.GetFormats(autoConvert: false);
                if (formats is null || formats.Length == 0)
                {
                    return new ClipboardBackup(null, null, true);
                }

                return new ClipboardBackup(null, null, false);
            }
            catch (COMException) when (attempt < Attempts)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (COMException)
            {
                return new ClipboardBackup(null, null, false);
            }
        }

        return new ClipboardBackup(null, null, false);
    }

    public async Task<string?> TryReadSelectionAsync(
        nint sourceWindow,
        uint sourceProcessId,
        uint sequence,
        ClipboardBackup backup,
        CancellationToken cancellationToken = default)
    {
        _window ??= new HwndSource(new HwndSourceParameters("MiniTranslator.Clipboard")
        {
            ParentWindow = NativeMethods.HWND_MESSAGE,
            WindowStyle = 0,
        });

        for (var attempt = 1; !NativeMethods.OpenClipboard(_window.Handle); attempt++)
        {
            if (attempt >= Attempts)
            {
                return null;
            }

            await Task.Delay(RetryDelay, cancellationToken);
        }

        string? selected = null;
        try
        {
            NativeMethods.GetWindowThreadProcessId(NativeMethods.GetClipboardOwner(), out var ownerProcessId);
            if (!ClipboardCapture.IsExpected(sourceWindow, NativeMethods.GetForegroundWindow(),
                    sourceProcessId, ownerProcessId, sequence, NativeMethods.GetClipboardSequenceNumber()))
            {
                return null;
            }

            selected = ReadUnicodeText(NativeMethods.GetClipboardData(UnicodeText));
            if (selected is null || NativeMethods.GetForegroundWindow() != sourceWindow)
            {
                return null;
            }

            if (backup.Text is not null)
            {
                RestoreText(backup.Text, selected);
            }
            else if (backup.IsEmpty)
            {
                NativeMethods.EmptyClipboard();
            }

            return selected;
        }
        finally
        {
            NativeMethods.CloseClipboard();

            if (selected is not null && backup.OtherData is not null)
            {
                await RestoreNonTextAsync(backup.OtherData, cancellationToken);
            }
        }
    }

    private static async Task RestoreNonTextAsync(object otherData, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= Attempts; attempt++)
        {
            try
            {
                if (otherData is System.Windows.Media.Imaging.BitmapSource image)
                {
                    System.Windows.Clipboard.SetImage(image);
                }
                else if (otherData is System.Collections.Specialized.StringCollection files)
                {
                    System.Windows.Clipboard.SetFileDropList(files);
                }

                return;
            }
            catch (COMException) when (attempt < Attempts)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (COMException)
            {
                return;
            }
        }
    }

    public void Dispose()
    {
        _window?.Dispose();
        _window = null;
    }

    internal static unsafe string? ReadUnicodeText(nint memory)
    {
        if (memory == 0)
        {
            return null;
        }

        var length = NativeMethods.GlobalSize(memory) / sizeof(char);
        if (length == 0 || length > int.MaxValue)
        {
            return null;
        }

        var pointer = NativeMethods.GlobalLock(memory);
        if (pointer == 0)
        {
            return null;
        }

        try
        {
            var text = new ReadOnlySpan<char>((void*)pointer, (int)length);
            var end = text.IndexOf('\0');
            return end < 0 ? null : text[..end].ToString();
        }
        finally
        {
            NativeMethods.GlobalUnlock(memory);
        }
    }

    internal static unsafe nint AllocateText(string text)
    {
        var byteCount = (nuint)(text.Length + 1) * sizeof(char);
        var memory = NativeMethods.GlobalAlloc(0x0002, byteCount);
        if (memory == 0)
        {
            return 0;
        }

        var pointer = NativeMethods.GlobalLock(memory);
        if (pointer == 0)
        {
            NativeMethods.GlobalFree(memory);
            return 0;
        }

        try
        {
            var destination = (char*)pointer;
            if (text.Length > 0)
            {
                fixed (char* source = text)
                {
                    Buffer.MemoryCopy(source, destination, byteCount, (nuint)text.Length * sizeof(char));
                }
            }

            destination[text.Length] = '\0';
        }
        finally
        {
            NativeMethods.GlobalUnlock(memory);
        }

        return memory;
    }

    private static void RestoreText(string previousText, string selected)
    {
        nint previousMemory = 0;
        nint selectedMemory = 0;
        try
        {
            previousMemory = AllocateText(previousText);
            if (previousMemory == 0 || !NativeMethods.EmptyClipboard())
            {
                return;
            }

            if (NativeMethods.SetClipboardData(UnicodeText, previousMemory) != 0)
            {
                previousMemory = 0;
                return;
            }

            selectedMemory = AllocateText(selected);
            if (selectedMemory != 0 && NativeMethods.SetClipboardData(UnicodeText, selectedMemory) != 0)
            {
                selectedMemory = 0;
                return;
            }

            throw new COMException("클립보드 텍스트를 복원하지 못했습니다.", Marshal.GetHRForLastWin32Error());
        }
        finally
        {
            if (previousMemory != 0) NativeMethods.GlobalFree(previousMemory);
            if (selectedMemory != 0) NativeMethods.GlobalFree(selectedMemory);
        }
    }
}

internal static class ClipboardErrors
{
    public static Error Busy(string? detail) =>
        new Error("Clipboard.Busy", "클립보드를 사용할 수 없습니다. 잠시 후 다시 시도하세요.").WithDetail(detail);
}

internal sealed record ClipboardBackup(string? Text, object? OtherData, bool IsEmpty);
