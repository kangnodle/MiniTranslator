using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.Localization;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Wpf.Views;

namespace MiniTranslator.Wpf.Services;

/// <summary>
/// The app's resident shell: tray icon, global hotkey handling and window lifetimes.
/// </summary>
internal sealed class AppShell(
    Func<SettingsViewModel> createSettingsViewModel,
    SettingsStore settings,
    HotkeyManager hotkeys,
    CaptureSelectionUseCase captureSelection,
    TranslatorViewModel translatorViewModel) : IDisposable
{
    private TaskbarIcon? _tray;
    private TranslatorWindow? _translatorWindow;
    private SettingsWindow? _settingsWindow;
    private bool _handlingHotkey;

    public void Start()
    {
        translatorViewModel.SettingsRequested += (_, _) => ShowSettings();
        hotkeys.Pressed += OnHotkeyPressed;
        settings.Changed += OnSettingsChanged;

        _tray = CreateTrayIcon();
        UpdateTrayToolTip(settings.Current);

        // Pay the XAML-load and first-render cost now rather than on the first hotkey press.
        TranslatorWindow.Prewarm();

        var registered = hotkeys.Apply(settings.Current.Hotkey);
        if (registered.IsFailure)
        {
            _tray.ShowNotification(UiText.Get("HotkeyRegistrationFailed", settings.Current.UiLanguage),
                UiText.Error(registered.Error, settings.Current.UiLanguage), NotificationIcon.Warning);
            ShowSettings();
        }
        else
        {
            _tray.ShowNotification(
                UiText.Get("Running", settings.Current.UiLanguage),
                UiText.Format("OpenWithHotkey", settings.Current.UiLanguage, settings.Current.Hotkey.ToDisplayString()),
                NotificationIcon.Info);
        }
    }

    private TranslatorWindow TranslatorWindow =>
        _translatorWindow ??= new TranslatorWindow(translatorViewModel);

    public void ShowTranslator(string? text = null)
    {
        translatorViewModel.Prepare(text);
        TranslatorWindow.ShowCentered();
    }

    public void ShowSettings()
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow(createSettingsViewModel());
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Show();
        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
    }

    public void Dispose()
    {
        hotkeys.Pressed -= OnHotkeyPressed;
        settings.Changed -= OnSettingsChanged;
        hotkeys.Dispose();

        if (_translatorWindow is not null)
        {
            _translatorWindow.AllowClose = true;
            _translatorWindow.Close();
        }

        _tray?.Dispose();
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        if (_handlingHotkey)
        {
            return;
        }

        _handlingHotkey = true;
        try
        {
            // Pressing the hotkey again while the translator is focused toggles it off.
            if (_translatorWindow is { IsVisible: true, IsActive: true })
            {
                _translatorWindow.Hide();
                return;
            }

            // Appear immediately, but without focus: the selection is copied with Ctrl+C,
            // which must still reach the app the user selected text in.
            var window = TranslatorWindow;
            window.ShowInactive();

            var selectedText = await captureSelection.ExecuteAsync();
            translatorViewModel.Prepare(selectedText, autoTranslate: settings.Current.Behavior.AutoTranslateOnCapture);
            window.FocusInput();
        }
        finally
        {
            _handlingHotkey = false;
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings current) => UpdateTrayToolTip(current);

    private void UpdateTrayToolTip(AppSettings current)
    {
        if (_tray is not null)
        {
            _tray.ToolTipText = $"MiniTranslator ({current.Hotkey.ToDisplayString()})";
        }
    }

    private TaskbarIcon CreateTrayIcon()
    {
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("OpenTranslator", "", () => ShowTranslator()));
        menu.Items.Add(CreateMenuItem("Settings", "", ShowSettings));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Exit", "", () => System.Windows.Application.Current.Shutdown()));

        var tray = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico")),
            ContextMenu = menu,
            NoLeftClickDelay = true,
        };
        tray.TrayLeftMouseUp += (_, _) => ShowTranslator();
        tray.ForceCreate(enablesEfficiencyMode: false);
        return tray;
    }

    private static MenuItem CreateMenuItem(string textKey, string glyph, Action onClick)
    {
        var item = new MenuItem
        {
            Icon = new TextBlock
            {
                Text = glyph,
                FontFamily = (System.Windows.Media.FontFamily)System.Windows.Application.Current.FindResource("FluentIconFont"),
                FontSize = 14,
            },
        };
        item.SetResourceReference(MenuItem.HeaderProperty, textKey);
        item.Click += (_, _) => onClick();
        return item;
    }
}
