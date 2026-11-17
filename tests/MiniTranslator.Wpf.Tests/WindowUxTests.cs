using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Testing;
using MiniTranslator.Wpf;
using MiniTranslator.Wpf.Controls;
using MiniTranslator.Wpf.Views;

namespace MiniTranslator.Wpf.Tests;

public class WindowUxTests
{
    [Fact]
    public void Windows_support_themes_resizing_and_accessible_translation_states()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                VerifyWindows();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "WPF UI checks timed out.");
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static void VerifyWindows()
    {
        var app = new App();
        app.InitializeComponent();
        try
        {
            Color? previousCardColor = null;
            foreach (var theme in new[] { ThemeMode.Light, ThemeMode.Dark })
            {
                app.ThemeMode = theme;
                var repository = new InMemorySettingsRepository();
                var store = new SettingsStore(repository);
                var viewModel = new TranslatorViewModel(
                    new TranslateTextUseCase(new FakeTranslator()), new ChangeLanguagesUseCase(store),
                    store, new FakeClipboard());
                var translator = new TranslatorWindow(viewModel) { AllowClose = true };
                var hotkeys = new HotkeyManager(new FakeGlobalHotkey());
                var settingsViewModel = new SettingsViewModel(new SettingsEditor(store, hotkeys, new FakeStartupRegistration(), new FakeCodexAccount()));
                var settings = new SettingsWindow(settingsViewModel);
                try
                {
                    VerifyTranslator(translator, viewModel);
                    VerifySettings(settings, settingsViewModel);
                    var cardColor = ((SolidColorBrush)((Border)translator.FindName("ResultPanel")).Background).Color;
                    if (previousCardColor is Color previous)
                    {
                        Assert.NotEqual(previous, cardColor);
                    }
                    previousCardColor = cardColor;
                    VerifyThemes(app, translator, settings, settingsViewModel, store);
                    repository.FailSaveWith = new MiniTranslator.Domain.Common.Error("Disk", "디스크가 가득 찼습니다.");
                    var themeBox = (ComboBox)settings.FindName("ThemeBox");
                    themeBox.SetCurrentValue(ComboBox.SelectedValueProperty, AppTheme.SolarizedDark);
                    settingsViewModel.SetThemeCommand.ExecutionTask?.GetAwaiter().GetResult();
                    RefreshLayout(settings);
                    Assert.Equal(AppTheme.System, themeBox.SelectedValue);
                    Assert.Equal(ThemeMode.System, app.ThemeMode);
                    Assert.NotNull(settingsViewModel.ThemeError);
                    VerifyUiLanguages(app, translator, settings, settingsViewModel);
                }
                finally
                {
                    translator.Close();
                    settings.Close();
                    hotkeys.Dispose();
                }
            }
        }
        finally
        {
            app.Shutdown();
        }
    }

    private static void VerifyTranslator(TranslatorWindow window, TranslatorViewModel viewModel)
    {
        Assert.True(window.ShowInTaskbar);

        var resultPanel = (Border)window.FindName("ResultPanel");
        var input = (TextBox)window.FindName("SourceTextBox");
        var result = (TextBox)window.FindName("ResultTextBox");
        var clear = (Button)window.FindName("ClearButton");
        var translate = (Button)window.FindName("TranslateButton");
        var sourcePlaceholder = (TextBlock)window.FindName("SourcePlaceholder");
        var resultPlaceholder = (TextBlock)window.FindName("ResultPlaceholder");
        var errorBar = (Border)window.FindName("TranslationErrorBar");

        Layout(window, window.MinWidth, window.MinHeight);
        Assert.Equal(2, Grid.GetRow(resultPanel));
        Assert.Equal(0, Grid.GetColumn(resultPanel));
        VerifyTranslationFlow();
        Assert.True(input.ActualHeight >= 48);
        Assert.True(result.ActualHeight >= 48);
        Assert.Equal(Visibility.Visible, sourcePlaceholder.Visibility);
        Assert.Equal(Visibility.Visible, resultPlaceholder.Visibility);
        Assert.Equal(Visibility.Collapsed, errorBar.Visibility);
        Assert.True(result.IsReadOnly && result.IsTabStop);
        Assert.False(input.AcceptsTab);
        Assert.Equal(5000, input.MaxLength);
        Assert.Same(input.Style, System.Windows.Application.Current.FindResource(typeof(TextBox)));
        Assert.NotNull(resultPanel.Background);
        var labels = Descendants((DependencyObject)window.Content).OfType<Label>().ToArray();
        Assert.Equal(2, labels.Length);
        Assert.All(labels, label => Assert.Same(window.FindResource("TextFillColorPrimaryBrush"), label.Foreground));

        Layout(window, 900, window.MinHeight);
        Assert.Equal(0, Grid.GetRow(resultPanel));
        Assert.Equal(2, Grid.GetColumn(resultPanel));
        VerifyTranslationFlow();
        Assert.True(input.ActualWidth >= 280);
        Assert.True(result.ActualWidth >= 280);
        Layout(window, 600, 640);
        Assert.Equal(2, Grid.GetRow(resultPanel));

        viewModel.SourceText = "Hello";
        RefreshLayout(window);
        Assert.Equal(Visibility.Collapsed, sourcePlaceholder.Visibility);
        viewModel.IsTranslating = true;
        RefreshLayout(window);
        Assert.Equal("번역하고 있습니다…", resultPlaceholder.Text);
        Assert.Equal(Visibility.Visible, Descendants((DependencyObject)window.Content).OfType<ProgressBar>().Single().Visibility);
        viewModel.IsTranslating = false;
        viewModel.ErrorMessage = "네트워크 연결을 확인하세요.";
        Layout(window, window.MinWidth, window.MinHeight);
        RefreshLayout(window);
        Assert.True(input.ActualHeight >= 48);
        Assert.True(result.ActualHeight >= 48);
        Assert.Equal(Visibility.Visible, errorBar.Visibility);
        Assert.Equal("안내 메시지를 확인한 뒤 다시 번역하세요.", resultPlaceholder.Text);
        Assert.Equal(AutomationLiveSetting.Assertive,
            UIElementAutomationPeer.CreatePeerForElement(Descendants(errorBar).OfType<LiveTextBlock>().Single())!.GetLiveSetting());
        viewModel.ErrorMessage = null;
        viewModel.TranslatedText = "안녕하세요";
        RefreshLayout(window);
        Assert.Equal(Visibility.Collapsed, errorBar.Visibility);
        Assert.Equal(Visibility.Collapsed, resultPlaceholder.Visibility);
        Assert.Equal("안녕하세요", result.Text);

        void VerifyTranslationFlow()
        {
            Assert.Equal(new[] { 3, 4, 5, 6 }, new Control[] { clear, input, translate, result }
                .Select(System.Windows.Input.KeyboardNavigation.GetTabIndex));
            Assert.True(clear.TranslatePoint(new Point(0, clear.ActualHeight), window).Y
                <= input.TranslatePoint(new Point(), window).Y);
            Assert.True(input.TranslatePoint(new Point(0, input.ActualHeight), window).Y
                <= translate.TranslatePoint(new Point(), window).Y);
            if (Grid.GetRow(resultPanel) == 2)
            {
                Assert.True(translate.TranslatePoint(new Point(0, translate.ActualHeight), window).Y
                    <= result.TranslatePoint(new Point(), window).Y);
            }
        }
    }

    private static void VerifySettings(SettingsWindow window, SettingsViewModel viewModel)
    {
        Layout(window, 560, 480);
        Assert.True(((HotkeyBox)window.FindName("HotkeyBox")).ActualWidth >= 250);
        var checkboxes = Descendants((DependencyObject)window.Content).OfType<CheckBox>().ToArray();
        Assert.Equal(4, checkboxes.Length);
        Assert.All(checkboxes, control => Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(control))));
        var startup = (CheckBox)window.FindName("StartWithWindowsCheckBox");
        Assert.True(startup.IsEnabled);
        startup.IsChecked = true;
        Assert.True(viewModel.StartWithWindows);
        viewModel.SaveError = "설정을 저장하지 못했습니다. 다시 시도하세요.";
        RefreshLayout(window);
        Assert.Equal(Visibility.Visible, ((Border)window.FindName("SaveErrorBar")).Visibility);
        var buttons = Descendants((DependencyObject)window.Content).OfType<Button>().ToArray();
        Assert.Single(buttons, button => button.IsDefault);
        Assert.Single(buttons, button => button.IsCancel);
        Assert.All(buttons.Where(button => button.IsDefault || button.IsCancel), button => Assert.True(button.ActualWidth >= 120));
    }

    private static void VerifyUiLanguages(App app, TranslatorWindow translator, SettingsWindow settings, SettingsViewModel viewModel)
    {
        var languageBox = (ComboBox)settings.FindName("UiLanguageBox");
        Assert.Equal(2, languageBox.Items.Count);
        Assert.Equal(UiLanguage.Korean, languageBox.SelectedValue);
        languageBox.SelectedValue = UiLanguage.English;
        Assert.Equal(UiLanguage.English, viewModel.UiLanguage);
        Assert.Equal("MiniTranslator 설정", settings.Title);

        foreach (var language in new[] { UiLanguage.English, UiLanguage.Korean })
        {
            app.ApplyUiLanguage(language);
            Layout(settings, settings.MinWidth, settings.MinHeight);
            Layout(translator, translator.MinWidth, translator.MinHeight);
            Assert.Equal(language == UiLanguage.English ? "MiniTranslator Settings" : "MiniTranslator 설정", settings.Title);
            var saveButton = Descendants((DependencyObject)settings.Content).OfType<Button>().Single(button => button.IsDefault);
            var saveText = Descendants(saveButton).OfType<TextBlock>().Single();
            Assert.Equal(language == UiLanguage.English ? "Save" : "저장", saveText.Text);
            Assert.Equal("Alt+S", AutomationProperties.GetAccessKey(saveButton));
            Assert.Contains(settings.InputBindings.OfType<System.Windows.Input.KeyBinding>(),
                binding => binding.Key == System.Windows.Input.Key.S && binding.Modifiers == System.Windows.Input.ModifierKeys.Alt
                    && binding.Command == viewModel.SaveCommand);
            Assert.Equal(language == UiLanguage.English ? "Text to translate" : "번역할 텍스트",
                AutomationProperties.GetName((TextBox)translator.FindName("SourceTextBox")));
            Assert.Equal(language == UiLanguage.English ? "Start when I sign in to Windows" : "Windows 로그인 시 자동 실행",
                AutomationProperties.GetName((CheckBox)settings.FindName("StartWithWindowsCheckBox")));
            Assert.Equal(language == UiLanguage.English ? "en-us" : "ko-kr", settings.Language.IetfLanguageTag);
            var copyright = (TextBlock)translator.FindName("CopyrightText");
            var copyrightText = new TextRange(copyright.ContentStart, copyright.ContentEnd).Text;
            Assert.Contains("© 2026 kangnodle@gmail.com", copyrightText);
            Assert.Empty(copyright.Inlines.OfType<Hyperlink>());
            Assert.Contains(language == UiLanguage.English ? "All rights reserved." : "모든 권리 보유.", copyrightText);
            Assert.Equal(TextWrapping.Wrap, copyright.TextWrapping);
            var languageName = Descendants((ComboBox)translator.FindName("TargetLanguageBox")).OfType<TextBlock>()
                .First(text => text.DataContext is MiniTranslator.Domain.Languages.Language);
            Assert.Equal(language == UiLanguage.English ? "Korean" : "한국어 (Korean)", languageName.Text);
            Assert.True(((TextBox)translator.FindName("SourceTextBox")).ActualHeight >= 48);
        }
    }

    private static void VerifyThemes(App app, TranslatorWindow translator, SettingsWindow settings,
        SettingsViewModel viewModel, SettingsStore store)
    {
        var combo = (ComboBox)settings.FindName("ThemeBox");
        Assert.Equal(Enum.GetValues<AppTheme>(), combo.Items.Cast<ComboBoxItem>().Select(item => (AppTheme)item.Tag));
        Assert.Equal(AppTheme.System, combo.SelectedValue);
        var peer = UIElementAutomationPeer.CreatePeerForElement(combo)!;
        Assert.False(string.IsNullOrEmpty(peer.GetName()));
        Assert.IsAssignableFrom<ISelectionProvider>(peer.GetPattern(PatternInterface.Selection));
        var colors = new Dictionary<AppTheme, Color>();
        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light, AppTheme.TomorrowNightBlue,
                     AppTheme.SolarizedLight, AppTheme.SolarizedDark, AppTheme.Light, AppTheme.System })
        {
            combo.SetCurrentValue(ComboBox.SelectedValueProperty, theme);
            viewModel.SetThemeCommand.ExecutionTask?.GetAwaiter().GetResult();
            RefreshLayout(settings);
            RefreshLayout(translator);

            Assert.Equal(theme, viewModel.Theme);
            Assert.Equal(theme, store.Current.Theme);
            Assert.Equal(theme, combo.SelectedValue);
            Assert.Equal(theme switch
            {
                AppTheme.Light or AppTheme.SolarizedLight => ThemeMode.Light,
                AppTheme.Dark or AppTheme.TomorrowNightBlue or AppTheme.SolarizedDark => ThemeMode.Dark,
                _ => ThemeMode.System,
            }, app.ThemeMode);
            if (theme != AppTheme.System)
            {
                var cardColor = ((SolidColorBrush)((Border)translator.FindName("ResultPanel")).Background).Color;
                Assert.Equal(cardColor, ((SolidColorBrush)settings.FindResource("CardBackgroundFillColorDefaultBrush")).Color);
                if (colors.TryGetValue(theme, out var previous)) Assert.Equal(previous, cardColor);
                colors[theme] = cardColor;
            }
            if (theme is AppTheme.TomorrowNightBlue or AppTheme.SolarizedLight or AppTheme.SolarizedDark)
            {
                Assert.Equal(((SolidColorBrush)settings.FindResource("WindowBackground")).Color, ((SolidColorBrush)settings.Background).Color);
                Assert.Equal(settings.Background, translator.Background);
                Assert.Equal(((SolidColorBrush)settings.FindResource("ComboBoxBackground")).Color, ((SolidColorBrush)combo.Background).Color);
                var hotkey = (TextBox)settings.FindName("HotkeyBox");
                Assert.Equal(((SolidColorBrush)settings.FindResource("TextControlBackground")).Color, ((SolidColorBrush)hotkey.Background).Color);
                Assert.Equal(((SolidColorBrush)settings.FindResource("TextControlForeground")).Color, ((SolidColorBrush)hotkey.Foreground).Color);
                Assert.NotEqual(((SolidColorBrush)hotkey.Foreground).Color, ((SolidColorBrush)hotkey.Background).Color);
                foreach (var (foreground, background) in new[]
                         {
                             ("TextFillColorPrimaryBrush", "CardBackgroundFillColorDefaultBrush"),
                             ("TextFillColorSecondaryBrush", "CardBackgroundFillColorDefaultBrush"),
                             ("ComboBoxForeground", "ComboBoxBackgroundPointerOver"),
                             ("ComboBoxForegroundPressed", "ComboBoxBackgroundPressed"),
                             ("AccentButtonForeground", "AccentButtonBackground"),
                             ("SystemFillColorCriticalBrush", "SystemFillColorCriticalBackgroundBrush"),
                         })
                {
                    Assert.True(Contrast(((SolidColorBrush)settings.FindResource(foreground)).Color,
                        ((SolidColorBrush)settings.FindResource(background)).Color) >= 4.5, $"{theme}: {foreground} on {background}");
                }
            }
        }
        Assert.Equal(5, colors.Values.Distinct().Count());
    }

    private static double Contrast(Color first, Color second)
    {
        static double Linear(byte value)
        {
            var channel = value / 255.0;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }
    private static void Layout(Window window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        var content = (FrameworkElement)window.Content;
        // Leave room for the native window frame while measuring the real XAML.
        var size = new Size(width - 16, height - 40);
        content.Measure(size);
        content.Arrange(new Rect(size));
        RefreshLayout(window);
    }

    private static void RefreshLayout(Window window)
    {
        ((FrameworkElement)window.Content).UpdateLayout();
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
