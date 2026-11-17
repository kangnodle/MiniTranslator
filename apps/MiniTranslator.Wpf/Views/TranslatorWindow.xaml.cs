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

    public TranslatorWindow(TranslatorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Deactivated += (_, _) =>
        {
            if (viewModel.HideOnDeactivate && IsVisible)
            {
                Hide();
            }
        };
    }

    public bool AllowClose { get; set; }

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
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
