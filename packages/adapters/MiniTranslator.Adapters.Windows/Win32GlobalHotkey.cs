using System.Windows.Interop;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;

namespace MiniTranslator.Adapters.Windows;

/// <summary>
/// System-wide hotkey via <c>RegisterHotKey</c>, delivered to a private message-only window
/// so it does not depend on any visible UI window. Must be used from the UI (dispatcher) thread.
/// </summary>
internal sealed class Win32GlobalHotkey : IGlobalHotkey
{
    private const int HotkeyId = 0x4D54;      // "MT"
    private const int AliasHotkeyId = 0x4D55;

    private HwndSource? _window;
    private bool _registered;
    private bool _aliasRegistered;

    public event EventHandler? Pressed;

    public Result<Unit> Register(HotkeyGesture gesture)
    {
        var window = EnsureWindow();
        Unregister();

        var modifiers = (uint)gesture.Modifiers | NativeMethods.MOD_NOREPEAT;
        if (!NativeMethods.RegisterHotKey(window.Handle, HotkeyId, modifiers, (uint)gesture.VirtualKey))
        {
            return HotkeyErrors.AlreadyInUse(gesture);
        }

        _registered = true;

        // Best effort: the gesture already works wherever the layout reports the key unchanged.
        if (KeyboardLayoutAliases.For(gesture) is { } aliasKey)
        {
            _aliasRegistered = NativeMethods.RegisterHotKey(window.Handle, AliasHotkeyId, modifiers, (uint)aliasKey);
        }

        return Result.Success();
    }

    public void Unregister()
    {
        if (_window is null)
        {
            return;
        }

        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, HotkeyId);
            _registered = false;
        }

        if (_aliasRegistered)
        {
            NativeMethods.UnregisterHotKey(_window.Handle, AliasHotkeyId);
            _aliasRegistered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        _window?.RemoveHook(WndProc);
        _window?.Dispose();
        _window = null;
    }

    private HwndSource EnsureWindow()
    {
        if (_window is null)
        {
            _window = new HwndSource(new HwndSourceParameters("MiniTranslator.Hotkey")
            {
                ParentWindow = NativeMethods.HWND_MESSAGE,
                WindowStyle = 0,
            });
            _window.AddHook(WndProc);
        }

        return _window;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam is HotkeyId or AliasHotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }

        return nint.Zero;
    }
}
