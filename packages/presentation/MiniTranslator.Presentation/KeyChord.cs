using MiniTranslator.Domain.Hotkeys;

namespace MiniTranslator.Presentation;

/// <summary>
/// A key combination the user pressed, not yet validated.
/// UI controls report chords; turning one into a <see cref="HotkeyGesture"/> is the domain's job.
/// </summary>
public readonly record struct KeyChord(HotkeyModifiers Modifiers, int VirtualKey);
