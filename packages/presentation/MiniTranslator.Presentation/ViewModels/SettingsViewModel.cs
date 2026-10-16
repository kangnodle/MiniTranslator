using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.Localization;

namespace MiniTranslator.Presentation.ViewModels;

/// <summary>Edits a settings draft; the theme is applied and saved immediately.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsEditor _editor;

    private LanguagePair _languages;
    private bool _syncingLanguages;

    public SettingsViewModel(SettingsEditor editor)
    {
        _editor = editor;

        var startupState = editor.StartupState;
        CanConfigureStartup = startupState.IsSuccess;
        _startWithWindows = startupState.GetValueOrDefault(false);
        _startupError = startupState.IsFailure ? LocalizeError(startupState.Error) : null;

        var current = editor.Original;
        _theme = current.Theme;
        _uiLanguage = current.UiLanguage;
        _hotkey = current.Hotkey;
        _languages = current.Languages;
        _sourceLanguage = _languages.Source;
        _targetLanguage = _languages.Target;
        _captureSelectionOnHotkey = current.Behavior.CaptureSelectionOnHotkey;
        _hideOnDeactivate = current.Behavior.HideOnDeactivate;
        _autoTranslateOnCapture = current.Behavior.AutoTranslateOnCapture;
    }

    public event EventHandler? CloseRequested;

    public event EventHandler<AppTheme>? ThemeChanged;

    public IReadOnlyList<Language> SourceLanguages => LanguageCatalog.SourceOptions;

    public IReadOnlyList<Language> TargetLanguages => LanguageCatalog.TargetOptions;

    public string DefaultHotkeyText => HotkeyGesture.Default.ToDisplayString();

    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private string? _themeError;

    [RelayCommand]
    private async Task SetThemeAsync(AppTheme theme)
    {
        if (!Enum.IsDefined(theme))
        {
            return;
        }

        ThemeError = null;
        Theme = theme;
        ThemeChanged?.Invoke(this, theme);

        var saved = await _editor.SetThemeAsync(theme);
        if (saved.IsFailure)
        {
            Theme = _editor.Current.Theme;
            ThemeChanged?.Invoke(this, Theme);
            ThemeError = LocalizeError(saved.Error);
        }
    }

    // ── Hotkey ────────────────────────────────────────────────────────────

    [ObservableProperty]
    private HotkeyGesture _hotkey;

    [ObservableProperty]
    private string? _hotkeyError;

    [RelayCommand]
    private void RecordHotkey(KeyChord chord)
    {
        var gesture = HotkeyGesture.Create(chord.Modifiers, chord.VirtualKey);
        if (gesture.IsSuccess)
        {
            Hotkey = gesture.Value;
            HotkeyError = null;
        }
        else
        {
            HotkeyError = LocalizeError(gesture.Error);
        }
    }

    [RelayCommand]
    private void ResetHotkey()
    {
        Hotkey = HotkeyGesture.Default;
        HotkeyError = null;
    }

    /// <summary>The live hotkey would swallow the keys the user is trying to record, so release it meanwhile.</summary>
    public void BeginHotkeyRecording() => _editor.BeginHotkeyRecording();

    public bool EndHotkeyRecording()
    {
        var resumed = _editor.EndHotkeyRecording();
        if (resumed.IsFailure)
        {
            HotkeyError = LocalizeError(resumed.Error);
        }

        return resumed.IsSuccess;
    }

    // ── Languages ─────────────────────────────────────────────────────────

    [ObservableProperty]
    private Language _sourceLanguage;

    [ObservableProperty]
    private Language _targetLanguage;

    [ObservableProperty]
    private string? _languageError;

    partial void OnSourceLanguageChanged(Language value)
    {
        if (!_syncingLanguages && value is not null)
        {
            ApplyLanguages(_languages.WithSource(value));
        }
    }

    partial void OnTargetLanguageChanged(Language value)
    {
        if (!_syncingLanguages && value is not null)
        {
            ApplyLanguages(_languages.WithTarget(value));
        }
    }

    [RelayCommand]
    private void SwapLanguages() => ApplyLanguages(_languages.Swap());

    private void ApplyLanguages(Result<LanguagePair> next)
    {
        if (next.IsSuccess)
        {
            _languages = next.Value;
            LanguageError = null;
        }
        else
        {
            LanguageError = LocalizeError(next.Error);
        }

        _syncingLanguages = true;
        try
        {
            SourceLanguage = _languages.Source;
            TargetLanguage = _languages.Target;
        }
        finally
        {
            _syncingLanguages = false;
        }
    }

    // ── Behavior ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private bool _captureSelectionOnHotkey;

    [ObservableProperty]
    private bool _hideOnDeactivate;

    [ObservableProperty]
    private bool _autoTranslateOnCapture;

    [ObservableProperty]
    private UiLanguage _uiLanguage;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string? _startupError;

    public bool CanConfigureStartup { get; }

    // ── Save / cancel ─────────────────────────────────────────────────────

    [ObservableProperty]
    private string? _saveError;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var draft = new AppSettings(
            Hotkey,
            _languages,
            new BehaviorSettings(CaptureSelectionOnHotkey, HideOnDeactivate, AutoTranslateOnCapture))
        { UiLanguage = UiLanguage };

        var saved = await _editor.SaveAsync(draft, StartWithWindows);
        if (saved.Settings.IsFailure)
        {
            SaveError = LocalizeError(saved.Settings.Error);
            if (saved.StartupRollbackError is { } startupError)
                SaveError += "\n" + UiText.Format("StartupRollbackFailed", _editor.Current.UiLanguage, LocalizeError(startupError));
            if (saved.HotkeyRollbackError is { } hotkeyError)
                SaveError += "\n" + UiText.Format("HotkeyRollbackFailed", _editor.Current.UiLanguage, LocalizeError(hotkeyError));
            return;
        }

        SaveError = null;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private string LocalizeError(Error error) => UiText.Error(error, _editor.Current.UiLanguage);
}
