using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Wpf.Views;

public partial class SettingsView : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;

        viewModel.CloseRequested += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);
        viewModel.ThemeChanged += (_, theme) => ((App)System.Windows.Application.Current).ApplyTheme(theme);
    }

    public event EventHandler? CloseRequested;

    public bool TryClose()
    {
        if (!_viewModel.EndHotkeyRecording())
        {
            return false;
        }

        _viewModel.AbandonCodexSignIn();
        return true;
    }

    private void OnEngineToolbarSizeChanged(object sender, SizeChangedEventArgs e)
    {
        var narrow = e.NewSize.Width < 440;
        EngineColumn.Width = new GridLength(narrow ? 1 : 0.85, GridUnitType.Star);
        ModelColumn.Width = narrow ? new GridLength(0) : new GridLength(1.2, GridUnitType.Star);
        EffortColumn.Width = narrow ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        EffortColumn.MinWidth = narrow ? 0 : 96;
        Place(ModelLabel, narrow ? 1 : 0, narrow ? 0 : 2);
        Place(ClaudeModelPanel, narrow ? 1 : 0, narrow ? 1 : 3);
        Place(CodexModelPanel, narrow ? 1 : 0, narrow ? 1 : 3);
        Place(EffortLabel, narrow ? 2 : 0, narrow ? 0 : 4);
        Place(ClaudeEffortPanel, narrow ? 2 : 0, narrow ? 1 : 5);
        Place(CodexEffortPanel, narrow ? 2 : 0, narrow ? 1 : 5);
        Grid.SetColumn(EngineHelpButton, narrow ? 2 : 6);
    }

    private static void Place(UIElement element, int row, int column)
    {
        Grid.SetRow(element, row);
        Grid.SetColumn(element, column);
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

    private void HotkeyBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _viewModel.BeginHotkeyRecording();

    private void HotkeyBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _viewModel.EndHotkeyRecording();
}
