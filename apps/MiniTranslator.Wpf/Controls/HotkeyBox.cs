using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Presentation;

namespace MiniTranslator.Wpf.Controls;

/// <summary>
/// A read-only text box that records a key combination.
/// It only reports what was pressed (<see cref="RecordCommand"/> with a <see cref="KeyChord"/>);
/// deciding whether that is a valid hotkey is the domain's job.
/// </summary>
public sealed class HotkeyBox : TextBox
{
    public static readonly DependencyProperty GestureProperty = DependencyProperty.Register(
        nameof(Gesture), typeof(HotkeyGesture), typeof(HotkeyBox),
        new PropertyMetadata(null, (d, _) => ((HotkeyBox)d).ShowGesture()));

    public static readonly DependencyProperty RecordCommandProperty = DependencyProperty.Register(
        nameof(RecordCommand), typeof(ICommand), typeof(HotkeyBox));

    public HotkeyBox()
    {
        // Implicit styles match the exact type, so borrow the theme's TextBox style explicitly.
        SetResourceReference(StyleProperty, typeof(TextBox));

        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        IsUndoEnabled = false;
        ContextMenu = null;
        Cursor = Cursors.Arrow;
        InputMethod.SetIsInputMethodEnabled(this, false);
    }

    public HotkeyGesture? Gesture
    {
        get => (HotkeyGesture?)GetValue(GestureProperty);
        set => SetValue(GestureProperty, value);
    }

    public ICommand? RecordCommand
    {
        get => (ICommand?)GetValue(RecordCommandProperty);
        set => SetValue(RecordCommandProperty, value);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        var key = e.Key switch
        {
            Key.System => e.SystemKey,
            Key.ImeProcessed => e.ImeProcessedKey,
            _ => e.Key,
        };
        var modifiers = CurrentModifiers();

        // Keep plain Tab / Shift+Tab for keyboard navigation.
        if (key == Key.Tab && (modifiers & ~HotkeyModifiers.Shift) == HotkeyModifiers.None)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        e.Handled = true;
        var virtualKey = KeyInterop.VirtualKeyFromKey(key);

        if (VirtualKeys.IsModifier(virtualKey))
        {
            Text = modifiers.ToPendingDisplayString();
            return;
        }

        var chord = new KeyChord(modifiers, virtualKey);
        if (RecordCommand?.CanExecute(chord) == true)
        {
            RecordCommand.Execute(chord);
        }

        ShowGesture();
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);
        if (CurrentModifiers() == HotkeyModifiers.None)
        {
            ShowGesture();
        }
    }

    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);
        ShowGesture();
    }

    private void ShowGesture() => Text = Gesture?.ToDisplayString() ?? string.Empty;

    private static HotkeyModifiers CurrentModifiers()
    {
        var wpf = Keyboard.Modifiers;
        var result = HotkeyModifiers.None;
        if (wpf.HasFlag(ModifierKeys.Control)) result |= HotkeyModifiers.Control;
        if (wpf.HasFlag(ModifierKeys.Alt)) result |= HotkeyModifiers.Alt;
        if (wpf.HasFlag(ModifierKeys.Shift)) result |= HotkeyModifiers.Shift;
        if (wpf.HasFlag(ModifierKeys.Windows) || Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
        {
            result |= HotkeyModifiers.Windows;
        }

        return result;
    }
}
