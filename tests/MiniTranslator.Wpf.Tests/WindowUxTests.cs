using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Common;
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
        var app = CreateAppWithoutStartup();
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
                var settings = new SettingsView(settingsViewModel) { MinWidth = 560, MinHeight = 480 };
                var settingsHost = new Window { Content = settings };
                try
                {
                    VerifyTranslator(translator, viewModel);
                    VerifyCodexModelSelection();
                    VerifySettings(settings, settingsViewModel);
                    VerifyAppFont(app, translator, settings, settingsViewModel, store);
                    var cardColor = ((SolidColorBrush)((Border)translator.FindName("ResultPanel")).Background).Color;
                    if (previousCardColor is Color previous)
                    {
                        Assert.NotEqual(previous, cardColor);
                    }
                    previousCardColor = cardColor;
                    VerifyThemes(app, translator, settings, settingsViewModel, store);
                    repository.FailSaveWith = new MiniTranslator.Domain.Common.Error("Disk", "디스크가 가득 찼습니다.");
                    var themeBox = (Selector)settings.FindName("ThemeBox");
                    themeBox.SetCurrentValue(Selector.SelectedValueProperty, AppTheme.SolarizedDark);
                    settingsViewModel.SetThemeCommand.ExecutionTask?.GetAwaiter().GetResult();
                    RefreshLayout(settings);
                    Assert.Equal(AppTheme.System, themeBox.SelectedValue);
                    Assert.Equal(ThemeMode.System, app.ThemeMode);
                    Assert.NotNull(settingsViewModel.ThemeError);
                    VerifyUiLanguages(app, translator, settings, settingsViewModel);
                    VerifySettingsNavigation(translator, viewModel, store, hotkeys, repository);
                }
                finally
                {
                    translator.Close();
                    settings.TryClose();
                    settingsHost.Close();
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
        Assert.Equal(WindowStyle.None, window.WindowStyle);

        var resultPanel = (Border)window.FindName("ResultPanel");
        var popupSurface = (Border)window.FindName("EnginePopupSurface");
        var enginePopup = (Popup)window.FindName("EngineQuickPopup");
        var input = (TextBox)window.FindName("SourceTextBox");
        var result = (TextBox)window.FindName("ResultTextBox");
        var clear = (Button)window.FindName("ClearButton");
        var translate = (Button)window.FindName("TranslateButton");
        var engineQuick = (ToggleButton)window.FindName("EngineQuickButton");
        var pin = (ToggleButton)window.FindName("PinButton");
        var close = (Button)window.FindName("CloseButton");
        var sourcePlaceholder = (TextBlock)window.FindName("SourcePlaceholder");
        var resultPlaceholder = (TextBlock)window.FindName("ResultPlaceholder");
        var errorBar = (Border)window.FindName("TranslationErrorBar");

        Layout(window, window.MinWidth, window.MinHeight);
        Assert.Equal(2, Grid.GetRow(resultPanel));
        Assert.Equal(0, Grid.GetColumn(resultPanel));
        VerifyTranslationFlow();
        Assert.True(input.ActualHeight >= 48);
        Assert.True(result.ActualHeight >= 48);
        Assert.True(engineQuick.ActualWidth > 0);
        Assert.True(pin.IsChecked == true);
        Assert.True(close.ActualWidth > 0);
        Assert.Equal(Visibility.Visible, sourcePlaceholder.Visibility);
        Assert.Equal(Visibility.Visible, resultPlaceholder.Visibility);
        Assert.Equal(Visibility.Collapsed, errorBar.Visibility);
        Assert.True(result.IsReadOnly && result.IsTabStop);
        Assert.False(input.AcceptsTab);
        Assert.Equal(5000, input.MaxLength);
        Assert.Same(input.Style, System.Windows.Application.Current.FindResource("SpotlightTextBoxStyle"));
        Assert.NotNull(resultPanel.Background);
        var sourceLang = (ComboBox)window.FindName("SourceLanguageBox");
        var targetLang = (ComboBox)window.FindName("TargetLanguageBox");
        Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(sourceLang)));
        Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(targetLang)));
        Assert.True(targetLang.TranslatePoint(new Point(targetLang.ActualWidth, 0), window).X
            <= engineQuick.TranslatePoint(new Point(), window).X);
        Assert.NotNull(window.TryFindResource("SystemFillColorSuccessBrush"));
        Assert.Equal(255, ((SolidColorBrush)popupSurface.Background).Color.A);
        Assert.NotNull(enginePopup.CustomPopupPlacementCallback);

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
            Assert.Equal(new[] { 3, 4, 5, 6 }, new Control[] { input, clear, translate, result }
                .Select(System.Windows.Input.KeyboardNavigation.GetTabIndex));
            Assert.True(input.TranslatePoint(new Point(0, input.ActualHeight), window).Y
                <= clear.TranslatePoint(new Point(), window).Y + 4);
            Assert.True(input.TranslatePoint(new Point(0, input.ActualHeight), window).Y
                <= translate.TranslatePoint(new Point(), window).Y + 4);
            if (Grid.GetRow(resultPanel) == 2)
            {
                Assert.True(translate.TranslatePoint(new Point(0, translate.ActualHeight), window).Y
                    <= result.TranslatePoint(new Point(), window).Y);
            }
        }
    }

    private static void VerifySettingsNavigation(TranslatorWindow window, TranslatorViewModel translator,
        SettingsStore store, HotkeyManager hotkeys, InMemorySettingsRepository repository)
    {
        repository.FailSaveWith = null;
        translator.SourceText = string.Join("\n", Enumerable.Range(1, 80).Select(index => $"Source line {index}"));
        translator.TranslatedText = string.Join("\n", Enumerable.Range(1, 80).Select(index => $"번역 결과 {index}"));
        window.Show();
        Layout(window, 440, 640);
        window.Left = 137;
        window.Top = 91;
        var source = (TextBox)window.FindName("SourceTextBox");
        var result = (TextBox)window.FindName("ResultTextBox");
        window.Activate();
        source.Focus();
        source.Select(12, 7);
        source.ScrollToVerticalOffset(120);
        result.ScrollToVerticalOffset(180);
        RefreshLayout(window);
        var sourceOffset = source.VerticalOffset;
        var resultOffset = result.VerticalOffset;
        Assert.True(sourceOffset > 0);
        Assert.True(resultOffset > 0);
        var bounds = new Rect(window.Left, window.Top, window.Width, window.Height);
        var sourceText = source.Text;
        var resultText = result.Text;

        foreach (var action in new[] { "cancel", "save", "back", "escape" })
        {
            var draft = new SettingsViewModel(new SettingsEditor(store, hotkeys,
                new FakeStartupRegistration(), new FakeCodexAccount()));
            window.ShowSettings(() => draft);
            RefreshLayout(window);
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            var settings = Descendants((DependencyObject)window.Content).OfType<SettingsView>().Single();
            Assert.Same(window, Window.GetWindow(settings));
            Assert.Equal(bounds, new Rect(window.Left, window.Top, window.Width, window.Height));
            Assert.False(source.IsVisible);
            Assert.True(settings.IsVisible);
            Assert.True(((ScrollViewer)settings.FindName("Scroller")).ActualHeight > 0);
            VerifyEngineToolbar(settings);
            var uiLanguageBox = (ComboBox)settings.FindName("UiLanguageBox");
            uiLanguageBox.Focus();
            uiLanguageBox.IsDropDownOpen = true;
            Assert.True(uiLanguageBox.IsDropDownOpen);
            PressEscape(uiLanguageBox);
            Assert.False(uiLanguageBox.IsDropDownOpen);
            Assert.Single(Descendants((DependencyObject)window.Content).OfType<SettingsView>());
            Assert.DoesNotContain(window.InputBindings.OfType<System.Windows.Input.KeyBinding>(),
                binding => binding.Command == translator.TranslateCommand);
            window.ShowSettings(() => throw new InvalidOperationException("Settings should reuse the open draft."));
            if (action == "save")
            {
                repository.FailSaveWith = new MiniTranslator.Domain.Common.Error("Disk", "Disk full");
                draft.CaptureSelectionOnHotkey = !draft.CaptureSelectionOnHotkey;
                draft.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
                Assert.NotNull(draft.SaveError);
                Assert.Single(Descendants((DependencyObject)window.Content).OfType<SettingsView>());
                repository.FailSaveWith = null;
                draft.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            }
            else if (action == "back")
            {
                var back = (Button)settings.FindName("BackButton");
                back.Command.Execute(back.CommandParameter);
            }
            else if (action == "escape")
            {
                PressEscape(window);
            }
            else
            {
                draft.CancelCommand.Execute(null);
            }

            RefreshLayout(window);
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            Assert.Empty(Descendants((DependencyObject)window.Content).OfType<SettingsView>());
            Assert.Equal(bounds, new Rect(window.Left, window.Top, window.Width, window.Height));
            Assert.Equal(sourceText, source.Text);
            Assert.Equal(resultText, result.Text);
            Assert.Equal(12, source.SelectionStart);
            Assert.Equal(7, source.SelectionLength);
            Assert.Equal(sourceOffset, source.VerticalOffset);
            Assert.Equal(resultOffset, result.VerticalOffset);
        }

        var delayedRepository = new DeferredSettingsRepository();
        var delayedStore = new SettingsStore(delayedRepository);
        var previousDraft = new SettingsViewModel(new SettingsEditor(delayedStore, hotkeys,
            new FakeStartupRegistration(), new FakeCodexAccount()));
        window.ShowSettings(() => previousDraft);
        previousDraft.CaptureSelectionOnHotkey = !previousDraft.CaptureSelectionOnHotkey;
        var previousSave = previousDraft.SaveCommand.ExecuteAsync(null);
        Assert.False(previousSave.IsCompleted);
        previousDraft.CancelCommand.Execute(null);
        var currentDraft = new SettingsViewModel(new SettingsEditor(store, hotkeys,
            new FakeStartupRegistration(), new FakeCodexAccount()));
        window.ShowSettings(() => currentDraft);
        delayedRepository.SaveCompletion.SetResult(Result.Success());
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        Assert.True(previousSave.IsCompleted);
        previousSave.GetAwaiter().GetResult();
        Assert.Same(currentDraft, Descendants((DependencyObject)window.Content).OfType<SettingsView>().Single().DataContext);
        currentDraft.CancelCommand.Execute(null);
    }

    private sealed class DeferredSettingsRepository : ISettingsRepository
    {
        public TaskCompletionSource<Result<Unit>> SaveCompletion { get; } = new();

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(AppSettings.Default);

        public Task<Result<Unit>> SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) =>
            SaveCompletion.Task;
    }

    private static App CreateAppWithoutStartup()
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        System.Windows.Threading.DispatcherOperation? startup = null;
        void CaptureStartup(object? sender, System.Windows.Threading.DispatcherHookEventArgs e)
        {
            if (e.Operation.Priority == System.Windows.Threading.DispatcherPriority.Send)
            {
                startup ??= e.Operation;
            }
        }

        dispatcher.Hooks.OperationPosted += CaptureStartup;
        try
        {
            var app = new App();
            Assert.NotNull(startup);
            Assert.True(startup.Abort());
            return app;
        }
        finally
        {
            dispatcher.Hooks.OperationPosted -= CaptureStartup;
        }
    }

    private static void PressEscape(UIElement target)
    {
        var key = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(target)!, 0, System.Windows.Input.Key.Escape)
        {
            RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent,
        };
        target.RaiseEvent(key);
        if (!key.Handled)
        {
            key.RoutedEvent = System.Windows.Input.Keyboard.KeyDownEvent;
            target.RaiseEvent(key);
        }
    }

    private static void VerifySettings(SettingsView window, SettingsViewModel viewModel)
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

    private static void VerifyUiLanguages(App app, TranslatorWindow translator, SettingsView settings, SettingsViewModel viewModel)
    {
        var languageBox = (ComboBox)settings.FindName("UiLanguageBox");
        Assert.Equal(2, languageBox.Items.Count);
        Assert.Equal(UiLanguage.Korean, languageBox.SelectedValue);
        languageBox.SelectedValue = UiLanguage.English;
        Assert.Equal(UiLanguage.English, viewModel.UiLanguage);
        Assert.Equal("설정", ((TextBlock)settings.FindName("SettingsHeading")).Text);

        foreach (var language in new[] { UiLanguage.English, UiLanguage.Korean })
        {
            app.ApplyUiLanguage(language);
            Layout(settings, settings.MinWidth, settings.MinHeight);
            Layout(translator, translator.MinWidth, translator.MinHeight);
            Assert.Equal(language == UiLanguage.English ? "Settings" : "설정", ((TextBlock)settings.FindName("SettingsHeading")).Text);
            VerifyEngineToolbar(settings);
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
            var copyright = (TextBlock)settings.FindName("CopyrightText");
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

    private static void VerifyAppFont(App app, TranslatorWindow translator, SettingsView settings,
        SettingsViewModel viewModel, SettingsStore store)
    {
        var picker = (ComboBox)settings.FindName("AppFontBox");
        var preview = (TextBlock)settings.FindName("AppFontPreview");
        var input = (TextBox)translator.FindName("SourceTextBox");
        var original = input.FontFamily;
        Assert.Equal(AppFont.System, picker.SelectedValue);
        picker.SetCurrentValue(ComboBox.SelectedValueProperty, AppFont.Batang);
        RefreshLayout(settings);
        Assert.Equal(AppFont.Batang, viewModel.Font);
        Assert.StartsWith("Batang", preview.FontFamily.Source);
        Assert.Equal(original, input.FontFamily);
        Assert.Equal(AppFont.System, store.Current.Font);

        app.ApplyFont(viewModel.Font);
        RefreshLayout(translator);
        RefreshLayout(settings);
        Assert.Equal(preview.FontFamily, input.FontFamily);
        Assert.Equal(preview.FontFamily, picker.FontFamily);

        picker.SetCurrentValue(ComboBox.SelectedValueProperty, AppFont.System);
        app.ApplyFont(AppFont.System);
        RefreshLayout(translator);
        Assert.Equal(original, input.FontFamily);
    }

    private static void VerifyCodexModelSelection()
    {
        var store = new SettingsStore(new InMemorySettingsRepository());
        using var hotkeys = new HotkeyManager(new FakeGlobalHotkey());
        foreach (var model in new[] { "gpt-6.1-terra", "custom-codex-model", string.Empty })
        {
            var viewModel = new SettingsViewModel(new SettingsEditor(store, hotkeys,
                new FakeStartupRegistration(), new FakeCodexAccount()));
            var window = new SettingsView(viewModel);
            var engines = (ComboBox)window.FindName("EngineBox");
            engines.SetCurrentValue(ComboBox.SelectedValueProperty, TranslationEngine.Codex);
            Layout(window, 700, 600);
            var models = Assert.IsType<ComboBox>(window.FindName("CodexModelBox"));
            Assert.True(models.IsEditable);
            Assert.Equal(store.Current.Codex.Model, models.Text);
            Assert.NotEmpty(models.Items);
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(models)));
            if (model == "gpt-6.1-terra")
            {
                models.SetCurrentValue(ComboBox.SelectedItemProperty,
                    models.Items.Cast<ComboBoxItem>().Single(item => Equals(item.Content, model)));
            }
            else
            {
                models.SetCurrentValue(ComboBox.TextProperty, model);
            }
            Assert.Equal(model, viewModel.CodexModel);
            viewModel.SaveCommand.ExecuteAsync(null).GetAwaiter().GetResult();
            Assert.Equal(model, store.Current.Codex.Model);
            window.TryClose();
        }
    }

    private static void VerifyEngineToolbar(SettingsView window)
    {
        var content = (FrameworkElement)window.Content;
        var engines = (ComboBox)window.FindName("EngineBox");
        var help = Assert.IsType<ToggleButton>(window.FindName("EngineHelpButton"));
        var hints = Assert.IsType<StackPanel>(window.FindName("EngineHelpPanel"));
        foreach (var engine in new[] { TranslationEngine.Codex, TranslationEngine.Claude })
        {
            engines.SetCurrentValue(ComboBox.SelectedValueProperty, engine);
            RefreshLayout(window);
            var model = (Control)window.FindName($"{engine}ModelBox");
            var effort = (ComboBox)window.FindName($"{engine}EffortBox");
            var controls = new Control[] { engines, model, effort, help };
            var rowCenter = engines.TranslatePoint(new Point(0, engines.ActualHeight / 2), content).Y;
            foreach (var control in controls)
            {
                var center = control.TranslatePoint(new Point(0, control.ActualHeight / 2), content);
                Assert.True(control.ActualWidth >= 24);
                if (content.ActualWidth >= 500)
                {
                    Assert.InRange(center.Y, rowCenter - 1, rowCenter + 1);
                }
                Assert.InRange(center.X, 0, content.ActualWidth - control.ActualWidth);
            }
            if (content.ActualWidth < 500)
            {
                Assert.True(model.TranslatePoint(new Point(), content).Y > engines.TranslatePoint(new Point(), content).Y);
                Assert.True(effort.TranslatePoint(new Point(), content).Y > model.TranslatePoint(new Point(), content).Y);
            }
            Assert.Equal(Visibility.Collapsed, hints.Visibility);
            help.IsChecked = true;
            RefreshLayout(window);
            Assert.Equal(Visibility.Visible, hints.Visibility);
            help.IsChecked = false;
        }
    }

    private static void VerifyThemes(App app, TranslatorWindow translator, SettingsView settings,
        SettingsViewModel viewModel, SettingsStore store)
    {
        var selector = (ListBox)settings.FindName("ThemeBox");
        var uiLanguageBox = (ComboBox)settings.FindName("UiLanguageBox");
        Assert.Equal(Enum.GetValues<AppTheme>(), selector.Items.Cast<ListBoxItem>().Select(item => (AppTheme)item.Tag));
        Assert.Equal(AppTheme.System, selector.SelectedValue);
        var peer = UIElementAutomationPeer.CreatePeerForElement(selector)!;
        Assert.False(string.IsNullOrEmpty(peer.GetName()));
        Assert.IsAssignableFrom<ISelectionProvider>(peer.GetPattern(PatternInterface.Selection));
        var colors = new Dictionary<AppTheme, Color>();
        foreach (var theme in new[] { AppTheme.Dark, AppTheme.Light, AppTheme.TomorrowNightBlue,
                     AppTheme.SolarizedLight, AppTheme.SolarizedDark, AppTheme.Light, AppTheme.System })
        {
            selector.SetCurrentValue(Selector.SelectedValueProperty, theme);
            viewModel.SetThemeCommand.ExecutionTask?.GetAwaiter().GetResult();
            RefreshLayout(settings);
            RefreshLayout(translator);

            Assert.Equal(theme, viewModel.Theme);
            Assert.Equal(theme, store.Current.Theme);
            Assert.Equal(theme, selector.SelectedValue);
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
                Assert.Equal(((SolidColorBrush)settings.FindResource("ComboBoxBackground")).Color, ((SolidColorBrush)uiLanguageBox.Background).Color);
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
                             ("SystemFillColorSuccessBrush", "SystemFillColorSuccessBackgroundBrush"),
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
    private static void Layout(ContentControl window, double width, double height)
    {
        window.Width = width;
        window.Height = height;
        var content = (FrameworkElement)window.Content;
        var size = new Size(width - 16, height - 40);
        content.Measure(size);
        content.Arrange(new Rect(size));
        RefreshLayout(window);
    }

    private static void RefreshLayout(ContentControl window)
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
