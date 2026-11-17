using MiniTranslator.Domain.Hotkeys;

namespace MiniTranslator.Presentation;

public readonly record struct KeyChord(HotkeyModifiers Modifiers, int VirtualKey);
