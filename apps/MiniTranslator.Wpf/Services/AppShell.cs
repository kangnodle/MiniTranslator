using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MiniTranslator.Adapters.Windows;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.Localization;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Wpf.Views;

namespace MiniTranslator.Wpf.Services;

internal sealed class AppShell(
    Func<SettingsViewModel> createSettingsViewModel,
    SettingsStore settings,
    HotkeyManager hotkeys,
    CaptureSelectionUseCase captureSelection,
    TranslatorViewModel translatorViewModel) : IDisposable
{
    private static readonly TimeSpan StartupIdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan HiddenIdleDelay = TimeSpan.FromSeconds(15);

    private Win32TrayIcon? _tray;
    private TranslatorWindow? _translatorWindow;
    private DispatcherTimer? _idleTimer;
    private bool _handlingHotkey;

    public void Start(bool initiallyVisible = false)
    {
        translatorViewModel.SettingsRequested += (_, _) => ShowSettings();
        hotkeys.Pressed += OnHotkeyPressed;
        settings.Changed += OnSettingsChanged;

        _tray = CreateTrayIcon();
        UpdateTrayToolTip(settings.Current);

        _idleTimer = new DispatcherTimer(DispatcherPriority.ApplicationIdle);
        _idleTimer.Tick += OnIdleTrimTick;

        var registered = hotkeys.Apply(settings.Current.Hotkey);
        if (registered.IsFailure)
        {
            _tray.ShowNotification(
                UiText.Get("HotkeyRegistrationFailed", settings.Current.UiLanguage),
                UiText.Error(registered.Error, settings.Current.UiLanguage),
                TrayNotificationKind.Warning);
            ShowSettings();
            return;
        }

        _tray.ShowNotification(
            UiText.Get("Running", settings.Current.UiLanguage),
            UiText.Format("OpenWithHotkey", settings.Current.UiLanguage, settings.Current.Hotkey.ToDisplayString()),
            TrayNotificationKind.Info);

        if (!initiallyVisible)
        {
            ProcessMemoryPolicy.SetEfficiencyMode(true);
            ScheduleIdleTrim(StartupIdleDelay);
        }
    }

    private TranslatorWindow TranslatorWindow
    {
        get
        {
            if (_translatorWindow is null)
            {
                _translatorWindow = new TranslatorWindow(translatorViewModel);
                _translatorWindow.IsVisibleChanged += OnTranslatorVisibilityChanged;
            }

            return _translatorWindow;
        }
    }

    public void ShowTranslator(string? text = null)
    {
        EnterActiveMode();
        translatorViewModel.Prepare(text);
        TranslatorWindow.ShowCentered();
    }

    public void ShowSettings()
    {
        EnterActiveMode();
        TranslatorWindow.ShowSettings(createSettingsViewModel);
    }

    public void Dispose()
    {
        if (_idleTimer is not null)
        {
            _idleTimer.Stop();
            _idleTimer.Tick -= OnIdleTrimTick;
            _idleTimer = null;
        }

        hotkeys.Pressed -= OnHotkeyPressed;
        settings.Changed -= OnSettingsChanged;
        hotkeys.Dispose();

        if (_translatorWindow is not null)
        {
            _translatorWindow.IsVisibleChanged -= OnTranslatorVisibilityChanged;
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
            if (_translatorWindow is { IsVisible: true, IsActive: true })
            {
                _translatorWindow.Hide();
                return;
            }

            EnterActiveMode();
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

    private void OnTranslatorVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            EnterActiveMode();
        }
        else if (e.NewValue is false)
        {
            ScheduleIdleTrim(HiddenIdleDelay);
        }
    }

    private void EnterActiveMode()
    {
        _idleTimer?.Stop();
        ProcessMemoryPolicy.SetEfficiencyMode(false);
    }

    private void ScheduleIdleTrim(TimeSpan delay)
    {
        if (_idleTimer is null)
        {
            return;
        }

        _idleTimer.Stop();
        _idleTimer.Interval = delay;
        _idleTimer.Start();
    }

    private void OnIdleTrimTick(object? sender, EventArgs e)
    {
        _idleTimer?.Stop();
        if (_translatorWindow is { IsVisible: true })
        {
            return;
        }

        ProcessMemoryPolicy.SetEfficiencyMode(true);
        ProcessMemoryPolicy.TrimWorkingSet();
    }

    private void OnSettingsChanged(object? sender, AppSettings current) => UpdateTrayToolTip(current);

    private void UpdateTrayToolTip(AppSettings current)
    {
        if (_tray is not null)
        {
            _tray.ToolTipText = $"MiniTranslator ({current.Hotkey.ToDisplayString()})";
        }
    }

    private Win32TrayIcon CreateTrayIcon()
    {
        var menu = new ContextMenu();
        menu.SetResourceReference(Control.FontFamilyProperty, "AppFontFamily");
        menu.Items.Add(CreateMenuItem("OpenTranslator", "\uF2B7", () => ShowTranslator()));
        menu.Items.Add(CreateMenuItem("Settings", "\uE713", ShowSettings));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Exit", "\uE7E8", () => System.Windows.Application.Current.Shutdown()));

        var iconBytes = ReadEmbeddedIconBytes();
        var tray = new Win32TrayIcon(iconBytes, menu);
        tray.LeftClicked += (_, _) => ShowTranslator();
        return tray;
    }

    private static byte[] ReadEmbeddedIconBytes()
    {
        var info = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/MiniTranslator;component/Assets/app.ico"));
        if (info is null)
        {
            return [];
        }

        using var stream = info.Stream;
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
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
