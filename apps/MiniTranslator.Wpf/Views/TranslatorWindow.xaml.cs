using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MiniTranslator.Adapters.Windows;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.ViewModels;

namespace MiniTranslator.Wpf.Views;

public partial class TranslatorWindow : Window
{
    private const double EnginePopupHorizontalMargin = 16;

    private IInputElement? _translationFocus;
    private Point _sourceScroll;
    private Point _resultScroll;
    private bool? _isWideLayout;
    private double _lastMaxLanguageWidth = 140;

    public TranslatorWindow(TranslatorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        EngineQuickPopup.CustomPopupPlacementCallback = PlaceEnginePopup;

        SourceInitialized += OnSourceInitialized;
        StateChanged += OnWindowStateChanged;
        Closed += OnWindowClosed;

        if (System.Windows.Application.Current is App app)
        {
            app.ThemeApplied += OnThemeApplied;
        }

        Deactivated += (_, _) =>
        {
            if (viewModel.HideOnDeactivate && IsVisible && SettingsHost.Content is null)
            {
                EngineQuickPopup.IsOpen = false;
                Hide();
            }
        };
    }

    public bool AllowClose { get; set; }

    public void ShowSettings(Func<SettingsViewModel> createViewModel)
    {
        EngineQuickPopup.IsOpen = false;
        if (SettingsHost.Content is null)
        {
            _translationFocus = TranslationView.IsKeyboardFocusWithin ? Keyboard.FocusedElement : SettingsButton;
            _sourceScroll = new Point(SourceTextBox.HorizontalOffset, SourceTextBox.VerticalOffset);
            _resultScroll = new Point(ResultTextBox.HorizontalOffset, ResultTextBox.VerticalOffset);
            var view = new SettingsView(createViewModel());
            view.CloseRequested += OnSettingsCloseRequested;
            SettingsHost.Content = view;
            TranslationView.Visibility = Visibility.Hidden;
            SettingsHost.Visibility = Visibility.Visible;
            SetResourceReference(TitleProperty, "SettingsTitle");
        }

        ShowInactive();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        ((SettingsView)SettingsHost.Content).Focus();
    }

    private void OnSettingsCloseRequested(object? sender, EventArgs e) => CloseSettings();

    private bool CloseSettings()
    {
        if (SettingsHost.Content is not SettingsView view)
        {
            return true;
        }

        if (!view.TryClose())
        {
            return false;
        }

        view.CloseRequested -= OnSettingsCloseRequested;
        SettingsHost.Content = null;
        SettingsHost.Visibility = Visibility.Collapsed;
        TranslationView.Visibility = Visibility.Visible;
        Title = "MiniTranslator";
        if (_translationFocus is not null)
        {
            Keyboard.Focus(_translationFocus);
            _translationFocus = null;
        }

        SourceTextBox.ScrollToHorizontalOffset(_sourceScroll.X);
        SourceTextBox.ScrollToVerticalOffset(_sourceScroll.Y);
        ResultTextBox.ScrollToHorizontalOffset(_resultScroll.X);
        ResultTextBox.ScrollToVerticalOffset(_resultScroll.Y);

        return true;
    }

    public void ShowCentered()
    {
        ShowInactive();
        FocusInput();
    }

    public void ShowInactive()
    {
        if (!IsVisible)
        {
            var area = SystemParameters.WorkArea;
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + Math.Max(0, (area.Height - Height) / 3);
        }

        ShowInTaskbar = true;
        Show();
    }

    public void FocusInput()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        if (!CloseSettings())
        {
            return;
        }

        SourceTextBox.Focus();
        SourceTextBox.SelectAll();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var theme = System.Windows.Application.Current is App app ? app.CurrentTheme : AppTheme.System;
        WindowBackdrop.ApplyAcrylic(this, theme);
    }

    private void OnThemeApplied(object? sender, AppTheme theme) => WindowBackdrop.ApplyAcrylic(this, theme);

    private void OnWindowStateChanged(object? sender, EventArgs e) =>
        RootChrome.Margin = WindowState == WindowState.Maximized ? new Thickness(8) : new Thickness(0);

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.ThemeApplied -= OnThemeApplied;
        }
    }

    private void OnClearClick(object sender, RoutedEventArgs e) => SourceTextBox.Focus();

    private void OnEnginePresetClick(object sender, RoutedEventArgs e) => EngineQuickPopup.IsOpen = false;

    private void OnCloseButtonClick(object sender, RoutedEventArgs e) => Hide();

    private void OnTopBarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var maxLanguageWidth = e.NewSize.Width < 500
            ? Math.Max(64, (e.NewSize.Width - 266) / 2)
            : 140;

        if (Math.Abs(_lastMaxLanguageWidth - maxLanguageWidth) < 0.5)
        {
            return;
        }

        _lastMaxLanguageWidth = maxLanguageWidth;
        SourceLanguageBox.MaxWidth = maxLanguageWidth;
        TargetLanguageBox.MaxWidth = maxLanguageWidth;
    }

    private static CustomPopupPlacement[] PlaceEnginePopup(Size popupSize, Size targetSize, Point offset) =>
    [
        new CustomPopupPlacement(
            new Point(targetSize.Width - popupSize.Width + EnginePopupHorizontalMargin, targetSize.Height),
            PopupPrimaryAxis.Horizontal),
    ];

    private void OnTranslationPanelsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= 720;
        if (_isWideLayout == wide)
        {
            return;
        }

        _isWideLayout = wide;
        Grid.SetRow(PanelDivider, wide ? 0 : 1);
        Grid.SetColumn(PanelDivider, wide ? 1 : 0);
        Grid.SetRow(ResultPanel, wide ? 0 : 2);
        Grid.SetColumn(ResultPanel, wide ? 2 : 0);
        PanelColumnGap.Width = new GridLength(wide ? 1 : 0);
        ResultColumn.Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        PanelRowGap.Height = new GridLength(wide ? 0 : 1);
        ResultRow.Height = wide ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SettingsHost.Content is null)
        {
            if (EngineQuickPopup.IsOpen)
            {
                EngineQuickPopup.IsOpen = false;
                e.Handled = true;
                return;
            }

            Hide();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SettingsHost.Content is not null)
        {
            CloseSettings();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!CloseSettings() && !AllowClose)
        {
            e.Cancel = true;
            base.OnClosing(e);
            return;
        }

        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
