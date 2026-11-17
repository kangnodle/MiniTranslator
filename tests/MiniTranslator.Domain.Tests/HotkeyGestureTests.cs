using MiniTranslator.Domain.Hotkeys;

namespace MiniTranslator.Domain.Tests;

public class HotkeyGestureTests
{
    [Fact]
    public void Default_is_ctrl_space()
    {
        Assert.Equal(HotkeyModifiers.Control, HotkeyGesture.Default.Modifiers);
        Assert.Equal(VirtualKeys.Space, HotkeyGesture.Default.VirtualKey);
        Assert.Equal("Ctrl + Space", HotkeyGesture.Default.ToDisplayString());
    }

    [Fact]
    public void Requires_a_modifier()
    {
        var result = HotkeyGesture.Create(HotkeyModifiers.None, 'T');
        Assert.Equal(HotkeyErrors.ModifierRequired, result.Error);
    }

    [Fact]
    public void Rejects_shift_only_because_it_would_hijack_typing()
    {
        var result = HotkeyGesture.Create(HotkeyModifiers.Shift, 'T');
        Assert.Equal(HotkeyErrors.ShiftOnly, result.Error);
    }

    [Theory]
    [InlineData(0x11)]
    [InlineData(0xA0)]
    [InlineData(0x5B)]
    public void Rejects_a_modifier_as_the_main_key(int virtualKey)
    {
        var result = HotkeyGesture.Create(HotkeyModifiers.Control, virtualKey);
        Assert.Equal(HotkeyErrors.KeyRequired, result.Error);
    }

    [Fact]
    public void Rejects_unsupported_keys()
    {
        var result = HotkeyGesture.Create(HotkeyModifiers.Control, VirtualKeys.Escape);
        Assert.Equal("Hotkey.UnsupportedKey", result.Error.Code);
    }

    [Theory]
    [InlineData("Ctrl+Space", HotkeyModifiers.Control, 0x20)]
    [InlineData("Ctrl+Shift+T", HotkeyModifiers.Control | HotkeyModifiers.Shift, 'T')]
    [InlineData("Alt+F12", HotkeyModifiers.Alt, 0x7B)]
    [InlineData("Win+Alt+Num5", HotkeyModifiers.Windows | HotkeyModifiers.Alt, 0x65)]
    [InlineData("control + alt + .", HotkeyModifiers.Control | HotkeyModifiers.Alt, 0xBE)]
    public void Parses_text(string text, HotkeyModifiers modifiers, int virtualKey)
    {
        var gesture = HotkeyGesture.Parse(text).Value;

        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(virtualKey, gesture.VirtualKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Space")]
    [InlineData("Ctrl+")]
    [InlineData("Hyper+Space")]
    [InlineData("Ctrl+NoSuchKey")]
    public void Fails_on_invalid_text(string? text)
    {
        Assert.True(HotkeyGesture.Parse(text).IsFailure);
    }

    [Fact]
    public void ToString_round_trips_through_Parse()
    {
        var original = HotkeyGesture.Create(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Windows, 0xBF).Value;

        var parsed = HotkeyGesture.Parse(original.ToString()).Value;

        Assert.Equal(original, parsed);
        Assert.Equal("Ctrl+Alt+Win+/", original.ToString());
    }
}
