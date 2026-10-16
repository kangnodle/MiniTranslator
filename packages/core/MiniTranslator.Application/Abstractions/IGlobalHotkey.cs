using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;

namespace MiniTranslator.Application.Abstractions;

/// <summary>A single system-wide hotkey registration.</summary>
public interface IGlobalHotkey : IDisposable
{
    event EventHandler? Pressed;

    /// <summary>Registers <paramref name="gesture"/>, replacing any previous registration.</summary>
    Result<Unit> Register(HotkeyGesture gesture);

    void Unregister();
}
