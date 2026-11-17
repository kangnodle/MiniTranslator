using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.Localization;
using MiniTranslator.Wpf.Services;

namespace MiniTranslator.Wpf;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\MiniTranslator.SingleInstance";

    private Mutex? _singleInstance;
    private ServiceProvider? _services;
    private AppShell? _shell;
    private UiLanguage? _uiLanguage;
    private ResourceDictionary? _themeResources;

    public App()
    {
        InitializeComponent();
        ApplyUiLanguage(UiLanguage.Korean);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        _services = CompositionRoot.Build();
        var settings = _services.GetRequiredService<SettingsStore>();
        await settings.InitializeAsync();
        ApplyTheme(settings.Current.Theme);
        ApplyUiLanguage(settings.Current.UiLanguage);
        settings.Changed += (_, current) => ApplyUiLanguage(current.UiLanguage);

        _singleInstance = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            MessageBox.Show(
                UiText.Get("AlreadyRunning", settings.Current.UiLanguage),
                "MiniTranslator", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _shell = _services.GetRequiredService<AppShell>();
        _shell.Start();

        if (e.Args.Contains("--show", StringComparer.OrdinalIgnoreCase)) _shell.ShowTranslator();
        if (e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase)) _shell.ShowSettings();
    }

    public void ApplyTheme(AppTheme theme)
    {
        if (_themeResources is not null) Resources.MergedDictionaries.Remove(_themeResources);
        ThemeMode = theme switch
        {
            AppTheme.Light or AppTheme.SolarizedLight => System.Windows.ThemeMode.Light,
            AppTheme.Dark or AppTheme.TomorrowNightBlue or AppTheme.SolarizedDark => System.Windows.ThemeMode.Dark,
            _ => System.Windows.ThemeMode.System,
        };
        _themeResources = ThemeResources.Create(theme);
        if (_themeResources is not null) Resources.MergedDictionaries.Add(_themeResources);
        Resources["AppSurfaceBrush"] = _themeResources?["WindowBackground"] ?? Brushes.Transparent;
    }

    public void ApplyUiLanguage(UiLanguage language)
    {
        if (_uiLanguage == language) return;
        CultureInfo.CurrentUICulture = UiText.Culture(language);
        CultureInfo.DefaultThreadCurrentUICulture = UiText.Culture(language);
        foreach (var entry in UiText.All(language)) Resources[entry.Key] = entry.Value;
        Resources["CurrentUiLanguage"] = language;
        Resources["UiCulture"] = XmlLanguage.GetLanguage(language == UiLanguage.English ? "en-US" : "ko-KR");
        _uiLanguage = language;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shell?.Dispose();
        _services?.Dispose();

        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            UiText.Format("UnexpectedError", _uiLanguage ?? UiLanguage.Korean, e.Exception.Message),
            "MiniTranslator", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
