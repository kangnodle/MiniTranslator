using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;

namespace MiniTranslator.Application.Hotkeys;

/// <summary>
/// Keeps track of which gesture is registered and restores the previous one when a change fails,
/// so the user is never left without a working hotkey.
/// </summary>
public sealed class HotkeyManager(IGlobalHotkey hotkey) : IDisposable
{
    private bool _suspended;

    public HotkeyGesture? Active { get; private set; }

    public event EventHandler? Pressed
    {
        add => hotkey.Pressed += value;
        remove => hotkey.Pressed -= value;
    }

    public Result<Unit> Apply(HotkeyGesture gesture)
    {
        if (gesture == Active && !_suspended)
        {
            return Result.Success();
        }

        var registered = hotkey.Register(gesture);
        if (registered.IsFailure)
        {
            if (Active is not null && !_suspended)
            {
                hotkey.Register(Active);
            }

            return registered;
        }

        Active = gesture;
        _suspended = false;
        return registered;
    }

    /// <summary>Temporarily releases the hotkey, e.g. while the user records a new one.</summary>
    public void Suspend()
    {
        if (Active is null || _suspended)
        {
            return;
        }

        hotkey.Unregister();
        _suspended = true;
    }

    public Result<Unit> Resume() =>
        _suspended && Active is not null ? Apply(Active) : Result.Success();

    public void Dispose() => hotkey.Dispose();
}
