using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.Windows;

public static class WindowBackdrop
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static void ApplyAcrylic(Window window, AppTheme theme)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return;
        }

        var dark = IsDarkTheme(theme) ? 1 : 0;
        var corners = NativeMethods.DWMWCP_ROUND;
        var backdrop = IsCustomSolidTheme(theme)
            ? NativeMethods.DWMSBT_NONE
            : NativeMethods.DWMSBT_TRANSIENTWINDOW;

        _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
        _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corners, sizeof(int));
        _ = NativeMethods.DwmSetWindowAttribute(handle, NativeMethods.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
    }

    private static bool IsCustomSolidTheme(AppTheme theme) =>
        theme is AppTheme.TomorrowNightBlue or AppTheme.SolarizedLight or AppTheme.SolarizedDark;

    private static bool IsDarkTheme(AppTheme theme) => theme switch
    {
        AppTheme.Dark or AppTheme.TomorrowNightBlue or AppTheme.SolarizedDark => true,
        AppTheme.Light or AppTheme.SolarizedLight => false,
        _ => IsSystemDarkMode(),
    };

    private static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }
}
