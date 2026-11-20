using System.Windows;
using System.Windows.Media;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Wpf;

internal static class ThemeResources
{
    private sealed record Palette(string Background, string Card, string Input, string Hover, string Pressed,
        string Foreground, string Secondary, string Disabled, string Border, string Accent, string OnAccent,
        string Error, string ErrorBackground, string Success, string SuccessBackground);

    public static ResourceDictionary? Create(AppTheme theme)
    {
        var palette = theme switch
        {
            AppTheme.TomorrowNightBlue => new Palette("#002451", "#001C40", "#001733", "#00346E", "#003F8E", "#FFFFFF", "#BBDAFF", "#7086A5", "#404F7D", "#BBDAFF", "#002451", "#FF9DA4", "#40243E", "#D1F1A9", "#1B3B2B"),
            AppTheme.SolarizedLight => new Palette("#FDF6E3", "#EEE8D5", "#FDF6E3", "#E4DDC8", "#DFCA88", "#42585E", "#556970", "#93A1A1", "#B49471", "#268BD2", "#00151A", "#B82724", "#FCE4D6", "#2B6A00", "#E3ECD6"),
            AppTheme.SolarizedDark => new Palette("#002B36", "#073642", "#002B36", "#0B3C48", "#073642", "#A6B4B4", "#93A1A1", "#586E75", "#586E75", "#268BD2", "#00151A", "#FF6C6B", "#412F35", "#93C43A", "#153D32"),
            _ => null,
        };
        if (palette is null) return null;

        var resources = new ResourceDictionary();
        Set(palette.Background, "ApplicationBackgroundBrush", "WindowBackground", "SolidBackgroundFillColorBaseBrush");
        Set(palette.Card, "CardBackgroundFillColorDefaultBrush", "CardBackgroundFillColorSecondaryBrush",
            "LayerFillColorDefaultBrush", "LayerFillColorAltBrush", "SolidBackgroundFillColorSecondaryBrush",
            "AcrylicBackgroundFillColorDefaultBrush", "ComboBoxDropDownBackground", "ContextMenuBackground");
        Set(palette.Input, "ControlFillColorDefaultBrush", "ControlFillColorInputActiveBrush", "ButtonBackground",
            "ComboBoxBackground", "TextControlBackground", "TextControlBackgroundFocused", "CheckBoxCheckBackgroundFillUnchecked");
        Set(palette.Hover, "ControlFillColorSecondaryBrush", "SubtleFillColorSecondaryBrush", "ButtonBackgroundPointerOver",
            "ComboBoxBackgroundPointerOver", "TextControlBackgroundPointerOver", "ComboBoxDropDownBackgroundPointerOver",
            "ComboBoxItemBackgroundSelected", "CheckBoxCheckBackgroundFillUncheckedPointerOver", "TextControlButtonBackgroundPointerOver");
        Set(palette.Pressed, "ControlFillColorTertiaryBrush", "SubtleFillColorTertiaryBrush", "ButtonBackgroundPressed",
            "ComboBoxBackgroundPressed", "ComboBoxDropDownBackgroundPointerPressed", "ComboBoxFocusedDropDownBackgroundPointerPressed",
            "CheckBoxCheckBackgroundFillUncheckedPressed", "TextControlButtonBackgroundPressed",
            "TextControlSelectionHighlightColor", "AccentFillColorSelectedTextBackgroundBrush");
        Set(palette.Foreground, "TextFillColorPrimaryBrush", "ButtonForeground", "ButtonForegroundPointerOver",
            "ComboBoxForeground", "ComboBoxForegroundPointerOver", "ComboBoxForegroundPressed", "ComboBoxForegroundFocused",
            "ComboBoxForegroundFocusedPressed", "ComboBoxDropDownForeground", "ComboBoxItemForeground", "ComboBoxItemForegroundSelected",
            "TextControlForeground", "TextControlForegroundPointerOver", "TextControlForegroundFocused",
            "CheckBoxForegroundUnchecked", "ContextMenuForeground", "TextOnAccentFillColorSelectedTextBrush");
        Set(palette.Secondary, "TextFillColorSecondaryBrush", "TextFillColorTertiaryBrush", "ButtonForegroundPressed",
            "ComboBoxDropDownGlyphForeground", "ComboBoxDropDownGlyphForegroundFocused", "ComboBoxDropDownGlyphForegroundFocusedPressed",
            "ComboBoxEditableDropDownGlyphForeground", "TextControlPlaceholderForeground", "TextControlButtonForeground",
            "TextControlButtonForegroundPointerOver", "TextControlButtonForegroundPressed", "ScrollBarThumbFill", "ScrollBarButtonArrowForeground");
        Set(palette.Disabled, "TextFillColorDisabledBrush", "ButtonForegroundDisabled", "AccentButtonForegroundDisabled",
            "ComboBoxForegroundDisabled", "ComboBoxDropDownGlyphForegroundDisabled", "TextControlForegroundDisabled",
            "CheckBoxForegroundUncheckedDisabled", "CheckBoxCheckGlyphForegroundDisabled");
        Set(palette.Card, "ControlFillColorDisabledBrush", "ButtonBackgroundDisabled", "AccentButtonBackgroundDisabled",
            "ComboBoxBackgroundDisabled", "TextControlBackgroundDisabled", "CheckBoxCheckBackgroundFillUncheckedDisabled");
        Set(palette.Border, "CardStrokeColorDefaultBrush", "ControlStrokeColorDefaultBrush", "ControlStrongStrokeColorDefaultBrush",
            "SurfaceStrokeColorFlyoutBrush", "DividerStrokeColorDefaultBrush", "ButtonBorderBrush", "ButtonBorderBrushPointerOver",
            "ButtonBorderBrushPressed", "ButtonBorderBrushDisabled", "ComboBoxBorderBrush", "ComboBoxBorderBrushPointerOver",
            "ComboBoxBorderBrushPressed", "ComboBoxBorderBrushDisabled", "ComboBoxDropDownBorderBrush", "ContextMenuBorderBrush",
            "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushDisabled",
            "CheckBoxCheckBackgroundStrokeUnchecked", "CheckBoxCheckBackgroundStrokeUncheckedPointerOver",
            "CheckBoxCheckBackgroundStrokeUncheckedPressed", "CheckBoxCheckBackgroundStrokeUncheckedDisabled", "ProgressBarBackground");
        Set(palette.Accent, "AccentFillColorDefaultBrush", "AccentFillColorSecondaryBrush", "AccentFillColorTertiaryBrush",
            "AccentTextFillColorPrimaryBrush", "AccentTextFillColorSecondaryBrush", "AccentTextFillColorTertiaryBrush",
            "KeyboardFocusBorderColorBrush", "FocusStrokeColorOuterBrush", "AccentButtonBackground", "AccentButtonBackgroundPointerOver",
            "AccentButtonBackgroundPressed", "AccentButtonBorderBrush", "AccentButtonBorderBrushPointerOver",
            "ComboBoxBorderBrushFocused", "ComboBoxItemPillFillBrush", "TextControlBorderBrushFocused", "TextControlFocusedBorderBrush",
            "ProgressBarForeground", "CheckBoxCheckBackgroundFillChecked", "CheckBoxCheckBackgroundFillCheckedPointerOver",
            "CheckBoxCheckBackgroundFillCheckedPressed", "CheckBoxCheckBackgroundStrokeChecked", "CheckBoxCheckBackgroundStrokeCheckedPointerOver",
            "CheckBoxCheckBackgroundStrokeCheckedPressed", "CheckBoxCheckBackgroundFillIndeterminate",
            "CheckBoxCheckBackgroundFillIndeterminatePointerOver", "CheckBoxCheckBackgroundFillIndeterminatePressed",
            "CheckBoxCheckBackgroundStrokeIndeterminate", "CheckBoxCheckBackgroundStrokeIndeterminatePointerOver", "CheckBoxCheckBackgroundStrokeIndeterminatePressed");
        Set(palette.OnAccent, "TextOnAccentFillColorPrimaryBrush", "TextOnAccentFillColorSecondaryBrush", "AccentButtonForeground",
            "AccentButtonForegroundPointerOver", "AccentButtonForegroundPressed", "CheckBoxCheckGlyphForeground", "CheckBoxCheckGlyphForegroundPressed");
        Set(palette.Error, "SystemFillColorCriticalBrush");
        Set(palette.ErrorBackground, "SystemFillColorCriticalBackgroundBrush");
        Set(palette.Success, "SystemFillColorSuccessBrush");
        Set(palette.SuccessBackground, "SystemFillColorSuccessBackgroundBrush");
        return resources;

        void Set(string color, params string[] keys)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            foreach (var key in keys) resources[key] = brush;
        }
    }
}
