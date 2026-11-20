using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.Localization;

namespace MiniTranslator.Wpf;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private AppContainer? _container;
    private UiLanguage? _uiLanguage;
    private ResourceDictionary? _languageResources;
    private ResourceDictionary? _themeResources;

    public AppTheme CurrentTheme { get; private set; } = AppTheme.System;

    public event EventHandler<AppTheme>? ThemeApplied;

    public App()
    {
        InitializeComponent();
        Resources[AppFont.System] = SystemFonts.MessageFontFamily;
        ApplyFont(AppFont.System);
        ApplyUiLanguage(UiLanguage.Korean);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var isFirstInstance = TryAcquireSingleInstanceMutex();
        if (!isFirstInstance)
        {
            var fallbackLanguage = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("en", StringComparison.OrdinalIgnoreCase)
                ? UiLanguage.English
                : UiLanguage.Korean;
            MessageBox.Show(
                UiText.Get("AlreadyRunning", fallbackLanguage),
                "MiniTranslator", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _container = AppContainer.Build();
        var settings = _container.Settings;
        await settings.InitializeAsync();
        ApplyTheme(settings.Current.Theme);
        ApplyDisplaySettings(settings.Current);
        settings.Changed += (_, current) => ApplyDisplaySettings(current);

        var showTranslator = e.Args.Contains("--show", StringComparer.OrdinalIgnoreCase);
        var showSettings = e.Args.Contains("--settings", StringComparer.OrdinalIgnoreCase);
        _container.Shell.Start(initiallyVisible: showTranslator || showSettings);

        if (showTranslator) _container.Shell.ShowTranslator();
        if (showSettings) _container.Shell.ShowSettings();
    }

    private void ApplyDisplaySettings(AppSettings settings)
    {
        ApplyUiLanguage(settings.UiLanguage);
        ApplyFont(settings.Font);
    }

    public void ApplyFont(AppFont font)
    {
        var family = (FontFamily)Resources[font];
        if (!Equals(Resources["AppFontFamily"], family)) Resources["AppFontFamily"] = family;
    }

    public void ApplyTheme(AppTheme theme)
    {
        var nextMode = theme switch
        {
            AppTheme.Light or AppTheme.SolarizedLight => System.Windows.ThemeMode.Light,
            AppTheme.Dark or AppTheme.TomorrowNightBlue or AppTheme.SolarizedDark => System.Windows.ThemeMode.Dark,
            _ => System.Windows.ThemeMode.System,
        };

        var needsCustomDictionary = theme is AppTheme.TomorrowNightBlue or AppTheme.SolarizedLight or AppTheme.SolarizedDark;
        if (CurrentTheme == theme && ThemeMode == nextMode && (needsCustomDictionary == (_themeResources is not null)))
        {
            return;
        }

        CurrentTheme = theme;
        if (_themeResources is not null)
        {
            Resources.MergedDictionaries.Remove(_themeResources);
            _themeResources = null;
        }

        if (ThemeMode != nextMode)
        {
            ThemeMode = nextMode;
        }

        _themeResources = ThemeResources.Create(theme);
        if (_themeResources is not null)
        {
            Resources.MergedDictionaries.Add(_themeResources);
        }

        Resources["AppSurfaceBrush"] = _themeResources?["WindowBackground"] ?? Brushes.Transparent;
        ThemeApplied?.Invoke(this, theme);
    }

    public void ApplyUiLanguage(UiLanguage language)
    {
        if (_uiLanguage == language) return;

        var culture = UiText.Culture(language);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        var nextResources = new ResourceDictionary();
        foreach (var entry in UiText.All(language))
        {
            nextResources[entry.Key] = entry.Value;
        }

        nextResources["CurrentUiLanguage"] = language;
        nextResources["UiCulture"] = XmlLanguage.GetLanguage(language == UiLanguage.English ? "en-US" : "ko-KR");

        if (_languageResources is not null)
        {
            Resources.MergedDictionaries.Remove(_languageResources);
        }

        Resources.MergedDictionaries.Add(nextResources);
        _languageResources = nextResources;
        _uiLanguage = language;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _container?.Dispose();
        _container = null;

        if (_singleInstance is not null)
        {
            _singleInstance.ReleaseMutex();
            _singleInstance.Dispose();
            _singleInstance = null;
        }

        base.OnExit(e);
    }

    private bool TryAcquireSingleInstanceMutex()
    {
        var mutexName = BuildSingleInstanceMutexName();
        try
        {
            var mutex = new Mutex(initiallyOwned: true, mutexName, out var isFirstInstance);
            if (!isFirstInstance)
            {
                mutex.Dispose();
                return false;
            }

            _singleInstance = mutex;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string BuildSingleInstanceMutexName()
    {
        var identity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"MiniTranslator:{identity}"));
        return $@"Local\MiniTranslator.SingleInstance.{Convert.ToHexString(hash.AsSpan(0, 8))}";
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            UiText.Format("UnexpectedError", _uiLanguage ?? UiLanguage.Korean, e.Exception.Message),
            "MiniTranslator", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
