using MiniTranslator.Application.Abstractions;

namespace MiniTranslator.Adapters.Windows;

public sealed class WindowsDesktopServices : IDisposable
{
    private readonly WpfClipboard _clipboard;

    internal WindowsDesktopServices(
        IGlobalHotkey hotkey,
        IStartupRegistration startup,
        WpfClipboard clipboard,
        ITextSelectionReader selectionReader)
    {
        Hotkey = hotkey;
        Startup = startup;
        _clipboard = clipboard;
        SelectionReader = selectionReader;
    }

    public IGlobalHotkey Hotkey { get; }

    public IStartupRegistration Startup { get; }

    public IClipboard Clipboard => _clipboard;

    public ITextSelectionReader SelectionReader { get; }

    public void Dispose()
    {
        Hotkey.Dispose();
        _clipboard.Dispose();
    }
}

public static class WindowsDesktopAdapter
{
    public static WindowsDesktopServices Create()
    {
        var clipboard = new WpfClipboard();
        return new WindowsDesktopServices(
            new Win32GlobalHotkey(),
            new WindowsStartupRegistration(),
            clipboard,
            new ClipboardSelectionReader(clipboard));
    }
}
