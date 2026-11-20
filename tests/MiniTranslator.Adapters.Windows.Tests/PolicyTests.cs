using MiniTranslator.Domain.Hotkeys;

namespace MiniTranslator.Adapters.Windows.Tests;

public class ClipboardCaptureTests
{
    [Theory]
    [InlineData(10, 10, 100, 100, 2, 2, true)]
    [InlineData(10, 10, 100, 200, 2, 2, false)]
    [InlineData(10, 10, 100, 0, 2, 2, false)]
    [InlineData(10, 20, 100, 100, 2, 2, false)]
    [InlineData(10, 10, 100, 100, 2, 3, false)]
    [InlineData(0, 0, 100, 100, 2, 2, false)]
    [InlineData(10, 10, 0, 0, 2, 2, false)]
    public void Only_accepts_the_expected_write_from_the_original_app(
        int sourceWindow, int foregroundWindow, uint sourceProcessId, uint ownerProcessId,
        uint expectedSequence, uint currentSequence, bool expected)
    {
        Assert.Equal(expected, ClipboardCapture.IsExpected(sourceWindow, foregroundWindow,
            sourceProcessId, ownerProcessId, expectedSequence, currentSequence));
    }
}

public class CopyShortcutTests
{
    [Fact]
    public void Types_ctrl_c_when_ctrl_is_not_held()
    {
        Assert.Equal(
            [
                new KeyStroke(CopyShortcut.ControlKey, KeyUp: false),
                new KeyStroke(CopyShortcut.CKey, KeyUp: false),
                new KeyStroke(CopyShortcut.CKey, KeyUp: true),
                new KeyStroke(CopyShortcut.ControlKey, KeyUp: true),
            ],
            CopyShortcut.Strokes(controlHeld: false, conflictingModifierHeld: false));
    }

    [Fact]
    public void Reuses_a_held_ctrl_without_releasing_it()
    {
        var strokes = CopyShortcut.Strokes(controlHeld: true, conflictingModifierHeld: false);

        Assert.Equal([new KeyStroke(CopyShortcut.CKey, false), new KeyStroke(CopyShortcut.CKey, true)], strokes);
        Assert.DoesNotContain(strokes, s => s.VirtualKey == CopyShortcut.ControlKey);
    }

    [Fact]
    public void Ctrl_is_not_a_conflicting_modifier()
    {
        Assert.DoesNotContain((int)CopyShortcut.ControlKey, CopyShortcut.ConflictingModifierKeys);
        Assert.All(CopyShortcut.ConflictingModifierKeys, vk => Assert.True(VirtualKeys.IsModifier(vk)));
    }

    [Theory]
    [InlineData(0x10)]
    [InlineData(0x12)]
    [InlineData(0x5B)]
    [InlineData(0x5C)]
    public void Shift_alt_and_both_windows_keys_conflict_with_copy(int heldKey)
    {
        Assert.Contains(heldKey, CopyShortcut.ConflictingModifierKeys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_conflicting_modifier_produces_no_key_events_even_if_ctrl_is_held(bool controlHeld)
    {
        Assert.Empty(CopyShortcut.Strokes(controlHeld, conflictingModifierHeld: true));
    }
}

public class KeyboardLayoutAliasesTests
{
    [Fact]
    public void Ctrl_space_is_aliased_to_hanja()
    {
        Assert.Equal(KeyboardLayoutAliases.Hanja, KeyboardLayoutAliases.For(HotkeyGesture.Default));
    }

    [Theory]
    [InlineData("Ctrl+Shift+Space")]
    [InlineData("Alt+Space")]
    [InlineData("Ctrl+T")]
    public void Other_gestures_have_no_alias(string gesture)
    {
        Assert.Null(KeyboardLayoutAliases.For(HotkeyGesture.Parse(gesture).Value));
    }
}

public class TerminalProcessesTests
{
    [Theory]
    [InlineData("cmd", true)]
    [InlineData("powershell", true)]
    [InlineData("powershell_ise", true)]
    [InlineData("pwsh", true)]
    [InlineData("windowsterminal", true)]
    [InlineData("wt", true)]
    [InlineData("conhost", true)]
    [InlineData("conemu64", true)]
    [InlineData("wezterm-gui", true)]
    [InlineData("putty", true)]
    [InlineData("warp", true)]
    [InlineData("ghostty", true)]
    [InlineData("kitty", true)]
    [InlineData("git-bash", true)]
    [InlineData("bash", true)]
    [InlineData("notepad", false)]
    [InlineData("code", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Detects_terminal_processes(string? processName, bool expected)
    {
        Assert.Equal(expected, TerminalProcesses.IsTerminalProcess(processName));
    }
}
