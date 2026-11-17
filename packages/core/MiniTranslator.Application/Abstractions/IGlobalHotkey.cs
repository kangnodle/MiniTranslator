using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;

namespace MiniTranslator.Application.Abstractions;

public interface IGlobalHotkey : IDisposable
{
    event EventHandler? Pressed;

    Result<Unit> Register(HotkeyGesture gesture);

    void Unregister();
}
