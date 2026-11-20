using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.Localization;

namespace MiniTranslator.Presentation.ViewModels;

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
        _engine = current.Engine;
        _codexExecutablePath = current.CodexExecutablePath;
        _claudeModel = current.Claude.Model;
        _claudeEffort = current.Claude.Effort;
        _codexModel = current.Codex.Model;
        _codexEffort = current.Codex.Effort;
        _hotkey = current.Hotkey;
        _languages = current.Languages;
        _sourceLanguage = _languages.Source;
        _targetLanguage = _languages.Target;
        _captureSelectionOnHotkey = current.Behavior.CaptureSelectionOnHotkey;
        _hideOnDeactivate = current.Behavior.HideOnDeactivate;
        _autoTranslateOnCapture = current.Behavior.AutoTranslateOnCapture;

        if (_engine == TranslationEngine.Codex)
        {
            RefreshCodexAccountCommand.Execute(null);
        }
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

    [ObservableProperty]
    private bool _captureSelectionOnHotkey;

    [ObservableProperty]
    private bool _hideOnDeactivate;

    [ObservableProperty]
    private bool _autoTranslateOnCapture;

    [ObservableProperty]
    private UiLanguage _uiLanguage;

    [ObservableProperty]
    private TranslationEngine _engine;

    [ObservableProperty]
    private string _codexExecutablePath;

    [ObservableProperty]
    private string _claudeModel;

    [ObservableProperty]
    private string _claudeEffort;

    [ObservableProperty]
    private string _codexModel;

    [ObservableProperty]
    private string _codexEffort;

    [ObservableProperty]
    private string? _codexAccountText;

    [ObservableProperty]
    private string? _codexAccountError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOfferCodexSignIn))]
    private bool _isCodexSignedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanOfferCodexSignIn))]
    private bool _isCodexSigningIn;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCodexAccountCommand), nameof(SignInToCodexCommand), nameof(SignOutOfCodexCommand))]
    private bool _isCodexBusy;

    public bool CanOfferCodexSignIn => !IsCodexSignedIn && !IsCodexSigningIn;

    private bool IsCodexIdle => !IsCodexBusy;

    partial void OnEngineChanged(TranslationEngine value)
    {
        if (value == TranslationEngine.Codex && RefreshCodexAccountCommand.CanExecute(null))
        {
            RefreshCodexAccountCommand.Execute(null);
        }
    }

    [RelayCommand(CanExecute = nameof(IsCodexIdle))]
    private Task RefreshCodexAccountAsync() =>
        RunCodexAsync("CodexAccountChecking", token => _editor.ReadCodexAccountAsync(CodexExecutablePath, token), CancellationToken.None);

    [RelayCommand(CanExecute = nameof(IsCodexIdle), IncludeCancelCommand = true)]
    private async Task SignInToCodexAsync(CancellationToken cancellationToken)
    {
        IsCodexSigningIn = true;
        try
        {
            await RunCodexAsync("CodexSignInWaiting", token => _editor.SignInToCodexAsync(CodexExecutablePath, token), cancellationToken);
        }
        finally
        {
            IsCodexSigningIn = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsCodexIdle))]
    private Task SignOutOfCodexAsync() =>
        RunCodexAsync("CodexSigningOut", token => _editor.SignOutOfCodexAsync(CodexExecutablePath, token), CancellationToken.None);

    private async Task RunCodexAsync(
        string progressText,
        Func<CancellationToken, Task<Result<CodexAccountState>>> operation,
        CancellationToken cancellationToken)
    {
        IsCodexBusy = true;
        CodexAccountError = null;
        CodexAccountText = UiText.Get(progressText, _editor.Current.UiLanguage);
        try
        {
            var state = await operation(cancellationToken);
            if (state.IsFailure)
            {
                CodexAccountText = null;
                CodexAccountError = LocalizeError(state.Error);
                return;
            }

            IsCodexSignedIn = state.Value.SignedIn;
            CodexAccountText = DescribeCodexAccount(state.Value);
        }
        catch (OperationCanceledException)
        {
            CodexAccountText = DescribeCodexAccount(CodexAccountState.SignedOut);
        }
        finally
        {
            IsCodexBusy = false;
        }
    }

    private string DescribeCodexAccount(CodexAccountState state)
    {
        var language = _editor.Current.UiLanguage;
        return state switch
        {
            { SignedIn: false } => UiText.Get("CodexSignedOut", language),
            { Email: { Length: > 0 } email } => UiText.Format("CodexSignedInAs", language, email),
            _ => UiText.Get("CodexSignedIn", language),
        };
    }

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string? _startupError;

    public bool CanConfigureStartup { get; }

    [ObservableProperty]
    private string? _saveError;

    [RelayCommand]
    private async Task SaveAsync()
    {
        var draft = new AppSettings(
            Hotkey,
            _languages,
            new BehaviorSettings(CaptureSelectionOnHotkey, HideOnDeactivate, AutoTranslateOnCapture))
        {
            UiLanguage = UiLanguage,
            Engine = Engine,
            CodexExecutablePath = CodexExecutablePath ?? string.Empty,
            Claude = EngineOptions.Create(ClaudeModel, ClaudeEffort),
            Codex = EngineOptions.Create(CodexModel, CodexEffort),
        };

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
        AbandonCodexSignIn();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel()
    {
        AbandonCodexSignIn();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void AbandonCodexSignIn() => SignInToCodexCancelCommand.Execute(null);

    private string LocalizeError(Error error) => UiText.Error(error, _editor.Current.UiLanguage);
}
