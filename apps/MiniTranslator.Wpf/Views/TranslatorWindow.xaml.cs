using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MiniTranslator.Presentation.ViewModels;

namespace MiniTranslator.Wpf.Views;

public partial class TranslatorWindow : Window
{
    private const double OffScreen = -32000;

    private bool _prewarming;
    private IInputElement? _translationFocus;
    private Point _sourceScroll;
    private Point _resultScroll;

    public TranslatorWindow(TranslatorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Deactivated += (_, _) =>
        {
            if (viewModel.HideOnDeactivate && IsVisible && SettingsHost.Content is null)
            {
                Hide();
            }
        };
    }

    public bool AllowClose { get; set; }

    public void ShowSettings(Func<SettingsViewModel> createViewModel)
    {
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

    public void Prewarm()
    {
        if (IsVisible)
        {
            return;
        }

        Left = OffScreen;
        Top = OffScreen;
        ShowInTaskbar = false;
        Show();
        _prewarming = true;

        Dispatcher.InvokeAsync(() =>
        {
            if (_prewarming)
            {
                _prewarming = false;
                Hide();
                ShowInTaskbar = true;
            }
        }, DispatcherPriority.ApplicationIdle);
    }

    public void ShowCentered()
    {
        ShowInactive();
        FocusInput();
    }

    public void ShowInactive()
    {
        if (!IsVisible || _prewarming)
        {
            _prewarming = false;

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

    private void OnClearClick(object sender, RoutedEventArgs e) => SourceTextBox.Focus();

    private void OnTranslationPanelsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var wide = e.NewSize.Width >= 720;
        Grid.SetRow(ResultPanel, wide ? 0 : 2);
        Grid.SetColumn(ResultPanel, wide ? 2 : 0);
        PanelColumnGap.Width = new GridLength(wide ? 12 : 0);
        ResultColumn.Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        PanelRowGap.Height = new GridLength(wide ? 0 : 12);
        ResultRow.Height = wide ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && SettingsHost.Content is null)
        {
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
