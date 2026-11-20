using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace MiniTranslator.Adapters.Windows;

public enum TrayNotificationKind
{
    Info,
    Warning,
}

public sealed class Win32TrayIcon : IDisposable
{
    private const uint TrayIconId = 1;
    private const uint CallbackMessage = NativeMethods.WM_APP + 1;
    private const uint IconFormatVersion = 0x00030000;

    private readonly ContextMenu _contextMenu;
    private readonly HwndSource _window;
    private readonly uint _taskbarCreatedMessage;
    private readonly nint _iconHandle;
    private string _toolTipText = string.Empty;
    private bool _disposed;

    public Win32TrayIcon(ReadOnlySpan<byte> iconBytes, ContextMenu contextMenu)
    {
        _contextMenu = contextMenu;
        _iconHandle = CreateIconHandle(iconBytes);
        _taskbarCreatedMessage = NativeMethods.RegisterWindowMessageW("TaskbarCreated");
        _window = new HwndSource(new HwndSourceParameters("MiniTranslator.Tray")
        {
            ParentWindow = NativeMethods.HWND_MESSAGE,
            WindowStyle = 0,
        });
        _window.AddHook(WndProc);
        AddIcon();
    }

    public event EventHandler? LeftClicked;

    public string ToolTipText
    {
        get => _toolTipText;
        set
        {
            var next = value ?? string.Empty;
            if (string.Equals(_toolTipText, next, StringComparison.Ordinal))
            {
                return;
            }

            _toolTipText = next;
            if (_disposed)
            {
                return;
            }

            var data = CreateBaseData(NativeMethods.NIF_TIP | NativeMethods.NIF_SHOWTIP);
            CopyFixedString(next, ref data, tip: true);
            _ = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_MODIFY, ref data);
        }
    }

    public void ShowNotification(string title, string message, TrayNotificationKind kind)
    {
        if (_disposed)
        {
            return;
        }

        var data = CreateBaseData(NativeMethods.NIF_INFO | NativeMethods.NIF_SHOWTIP);
        data.dwInfoFlags = kind == TrayNotificationKind.Warning ? NativeMethods.NIIF_WARNING : NativeMethods.NIIF_INFO;
        CopyNotificationStrings(title, message, ref data);
        _ = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var data = CreateBaseData(0);
        _ = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_DELETE, ref data);
        _window.RemoveHook(WndProc);
        _window.Dispose();
        if (_iconHandle != 0)
        {
            _ = NativeMethods.DestroyIcon(_iconHandle);
        }
    }

    internal static (int Offset, int Size)? SelectBestIconImage(ReadOnlySpan<byte> iconBytes, int desiredWidth, int desiredHeight)
    {
        if (iconBytes.Length < 6)
        {
            return null;
        }

        var reserved = BinaryPrimitives.ReadUInt16LittleEndian(iconBytes);
        var type = BinaryPrimitives.ReadUInt16LittleEndian(iconBytes[2..]);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(iconBytes[4..]);
        if (reserved != 0 || type != 1 || count == 0 || iconBytes.Length < 6 + count * 16)
        {
            return null;
        }

        (int Offset, int Size)? best = null;
        var bestScore = int.MaxValue;
        var bestBits = -1;

        for (var index = 0; index < count; index++)
        {
            var entry = iconBytes.Slice(6 + index * 16, 16);
            var width = entry[0] == 0 ? 256 : entry[0];
            var height = entry[1] == 0 ? 256 : entry[1];
            var bitCount = BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]);
            var size = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);

            if (size == 0 || offset > (uint)iconBytes.Length || size > (uint)iconBytes.Length - offset)
            {
                continue;
            }

            var penalty = (width < desiredWidth || height < desiredHeight ? 1000 : 0)
                          + Math.Abs(width - desiredWidth)
                          + Math.Abs(height - desiredHeight);

            if (penalty < bestScore || (penalty == bestScore && bitCount > bestBits))
            {
                bestScore = penalty;
                bestBits = bitCount;
                best = ((int)offset, (int)size);
            }
        }

        return best;
    }

    private static unsafe nint CreateIconHandle(ReadOnlySpan<byte> iconBytes)
    {
        var width = Math.Max(16, NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON));
        var height = Math.Max(16, NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSMICON));

        if (SelectBestIconImage(iconBytes, width, height) is not { } selected)
        {
            return 0;
        }

        fixed (byte* buffer = iconBytes)
        {
            return NativeMethods.CreateIconFromResourceEx(
                buffer + selected.Offset,
                (uint)selected.Size,
                fIcon: true,
                IconFormatVersion,
                width,
                height,
                0);
        }
    }

    private void AddIcon()
    {
        var flags = NativeMethods.NIF_MESSAGE | NativeMethods.NIF_TIP | NativeMethods.NIF_SHOWTIP;
        if (_iconHandle != 0)
        {
            flags |= NativeMethods.NIF_ICON;
        }

        var data = CreateBaseData(flags);
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _iconHandle;
        CopyFixedString(_toolTipText, ref data, tip: true);

        if (NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_ADD, ref data))
        {
            data.uTimeoutOrVersion = NativeMethods.NOTIFYICON_VERSION_4;
            _ = NativeMethods.Shell_NotifyIconW(NativeMethods.NIM_SETVERSION, ref data);
        }
    }

    private unsafe NativeMethods.NOTIFYICONDATAW CreateBaseData(uint flags) => new()
    {
        cbSize = (uint)sizeof(NativeMethods.NOTIFYICONDATAW),
        hWnd = _window.Handle,
        uID = TrayIconId,
        uFlags = flags,
    };

    private static unsafe void CopyFixedString(string value, ref NativeMethods.NOTIFYICONDATAW data, bool tip)
    {
        if (!tip)
        {
            return;
        }

        fixed (char* destination = data.szTip)
        {
            CopyTruncated(value, destination, 128);
        }
    }

    private static unsafe void CopyNotificationStrings(string title, string message, ref NativeMethods.NOTIFYICONDATAW data)
    {
        fixed (char* titleDest = data.szInfoTitle)
        {
            CopyTruncated(title, titleDest, 64);
        }

        fixed (char* infoDest = data.szInfo)
        {
            CopyTruncated(message, infoDest, 256);
        }
    }

    private static unsafe void CopyTruncated(string source, char* destination, int capacity)
    {
        var count = Math.Min(source.Length, capacity - 1);
        if (count > 0)
        {
            source.AsSpan(0, count).CopyTo(new Span<char>(destination, count));
        }

        destination[count] = '\0';
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (_taskbarCreatedMessage != 0 && (uint)msg == _taskbarCreatedMessage)
        {
            AddIcon();
            handled = true;
            return nint.Zero;
        }

        if (msg != (int)CallbackMessage)
        {
            return nint.Zero;
        }

        var trayEvent = (int)(lParam & 0xFFFF);
        switch (trayEvent)
        {
            case NativeMethods.WM_LBUTTONUP:
            case NativeMethods.NIN_SELECT:
            case NativeMethods.NIN_KEYSELECT:
                handled = true;
                LeftClicked?.Invoke(this, EventArgs.Empty);
                break;

            case NativeMethods.WM_RBUTTONUP:
            case NativeMethods.WM_CONTEXTMENU:
                handled = true;
                ShowContextMenu();
                break;
        }

        return nint.Zero;
    }

    private void ShowContextMenu()
    {
        _ = NativeMethods.SetForegroundWindow(_window.Handle);
        _contextMenu.Placement = PlacementMode.MousePoint;
        _contextMenu.IsOpen = true;
    }
}
