using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Wpf.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        viewModel.CloseRequested += (_, _) => Close();
        viewModel.ThemeChanged += (_, theme) => ((App)System.Windows.Application.Current).ApplyTheme(theme);
    }

    private void OnThemeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not SettingsViewModel model)
        {
            return;
        }

        var combo = (ComboBox)sender;
        if (combo.SelectedValue is AppTheme theme && theme != model.Theme && model.SetThemeCommand.CanExecute(theme))
        {
            model.SetThemeCommand.Execute(theme);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_viewModel.EndHotkeyRecording())
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        // Closing with the title bar skips Save and Cancel, and nobody is left to see the browser step finish.
        _viewModel.AbandonCodexSignIn();
        base.OnClosed(e);
    }

    private void HotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _viewModel.BeginHotkeyRecording();

    private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _viewModel.EndHotkeyRecording();
}
