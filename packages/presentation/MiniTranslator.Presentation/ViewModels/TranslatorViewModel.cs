using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Domain.Translations;
using MiniTranslator.Presentation.Localization;

namespace MiniTranslator.Presentation.ViewModels;

public sealed partial class TranslatorViewModel : ObservableObject
{
    private readonly TranslateTextUseCase _translate;
    private readonly ChangeLanguagesUseCase _changeLanguages;
    private readonly ChangeEngineUseCase _changeEngine;
    private readonly SettingsStore _settings;
    private readonly IClipboard _clipboard;

    private LanguagePair _languages;
    private Language? _detectedSource;
    private CancellationTokenSource? _pendingTranslation;
    private bool _syncingLanguages;
    private UiLanguage _uiLanguage;
    private Error? _lastError;
    private string? _infoKey;
    private string? _infoParameter;

    public TranslatorViewModel(
        TranslateTextUseCase translate,
        ChangeLanguagesUseCase changeLanguages,
        SettingsStore settings,
        IClipboard clipboard,
        ChangeEngineUseCase? changeEngine = null)
    {
        _translate = translate;
        _changeLanguages = changeLanguages;
        _changeEngine = changeEngine ?? new ChangeEngineUseCase(settings);
        _settings = settings;
        _clipboard = clipboard;

        _languages = settings.Current.Languages;
        _uiLanguage = settings.Current.UiLanguage;
        _selectedSource = _languages.Source;
        _selectedTarget = _languages.Target;

        settings.Changed += OnSettingsChanged;
    }

    public event EventHandler? SettingsRequested;

    public IReadOnlyList<Language> SourceLanguages => LanguageCatalog.SourceOptions;

    public IReadOnlyList<Language> TargetLanguages => LanguageCatalog.TargetOptions;

    public string HotkeyText => _settings.Current.Hotkey.ToDisplayString();

    public string CompactHotkeyText => HotkeyText.Replace(" + ", "+", StringComparison.Ordinal);

    public bool HideOnDeactivate => _settings.Current.Behavior.HideOnDeactivate;

    public TranslationEngine ActiveEngine => _settings.Current.Engine;

    public string ActiveEngineName => _settings.Current.Engine == TranslationEngine.Codex ? "Codex" : "Claude";

    public string ActiveModelName => _settings.Current.ActiveModelDisplay;

    public string ActiveEngineSummary => $"{ActiveEngineName} · {ActiveModelName}";

    public bool IsClaudeSonnetActive => ActiveEngine == TranslationEngine.Claude && string.Equals(ActiveModelName, "sonnet", StringComparison.OrdinalIgnoreCase);

    public bool IsClaudeOpusActive => ActiveEngine == TranslationEngine.Claude && string.Equals(ActiveModelName, "opus", StringComparison.OrdinalIgnoreCase);

    public bool IsClaudeHaikuActive => ActiveEngine == TranslationEngine.Claude && string.Equals(ActiveModelName, "haiku", StringComparison.OrdinalIgnoreCase);

    public bool IsCodexActive => ActiveEngine == TranslationEngine.Codex;

    public string CharacterCount => $"{SourceText.Length:N0} / {TranslationRequest.MaxLength:N0}";

    [ObservableProperty]
    private Language _selectedSource;

    [ObservableProperty]
    private Language _selectedTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCount))]
    [NotifyCanExecuteChangedFor(nameof(TranslateCommand), nameof(ClearCommand))]
    private string _sourceText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand), nameof(ClearCommand))]
    private string _translatedText = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string? _infoMessage;

    [ObservableProperty]
    private bool _isTranslating;

    [ObservableProperty]
    private bool _isCopied;

    public void Prepare(string? text, bool autoTranslate = true)
    {
        SetError(null);
        IsCopied = false;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        SourceText = text;
        TranslatedText = string.Empty;
        if (autoTranslate)
        {
            TranslateCommand.Execute(null);
        }
    }

    private bool CanTranslate() => !string.IsNullOrWhiteSpace(SourceText);

    [RelayCommand(CanExecute = nameof(CanTranslate), AllowConcurrentExecutions = true)]
    private async Task TranslateAsync()
    {
        _pendingTranslation?.Cancel();
        using var current = _pendingTranslation = new CancellationTokenSource();

        IsTranslating = true;
        IsCopied = false;
        SetError(null);
        SetInfo(null);

        var progress = new Progress<string>(partial =>
        {
            if (ReferenceEquals(_pendingTranslation, current) && !current.IsCancellationRequested)
            {
                TranslatedText = partial;
            }
        });

        try
        {
            var result = await _translate.ExecuteAsync(SourceText, _languages, progress, current.Token);
            if (!current.IsCancellationRequested)
            {
                ShowResult(result);
            }
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_pendingTranslation, current))
            {
                _pendingTranslation = null;
                IsTranslating = false;
            }
        }
    }

    private void ShowResult(Result<TranslationResult> result)
    {
        if (result.IsFailure)
        {
            SetError(result.Error);
            return;
        }

        var translation = result.Value;
        TranslatedText = translation.TranslatedText;
        _detectedSource = translation.DetectedSourceLanguage;
        SetInfo(translation.Languages.Source.IsAutoDetect && translation.DetectedSourceLanguageCode is not null ? "DetectedLanguage" : null, translation.DetectedSourceLanguageCode);
    }

    [RelayCommand]
    private void SwapLanguages()
    {
        var swapped = _languages.Swap(_detectedSource);
        if (swapped.IsFailure)
        {
            SetError(swapped.Error);
            return;
        }

        IsCopied = false;
        if (!string.IsNullOrWhiteSpace(TranslatedText))
        {
            SourceText = TranslatedText;
            TranslatedText = string.Empty;
        }

        ApplyLanguages(swapped);
    }

    private bool CanCopy() => !string.IsNullOrEmpty(TranslatedText);

    [RelayCommand(CanExecute = nameof(CanCopy))]
    private void Copy()
    {
        var copied = _clipboard.SetText(TranslatedText);
        if (copied.IsSuccess)
        {
            IsCopied = true;
            SetInfo("Copied");
        }
        else
        {
            IsCopied = false;
            SetError(copied.Error);
        }
    }

    private bool CanClear() => SourceText.Length > 0 || TranslatedText.Length > 0;

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear()
    {
        _pendingTranslation?.Cancel();
        SourceText = string.Empty;
        TranslatedText = string.Empty;
        IsCopied = false;
        SetError(null);
        SetInfo(null);
        _detectedSource = null;
    }

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task SelectEnginePresetAsync(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
        {
            return;
        }

        var parts = preset.Split(':', 2);
        if (!Enum.TryParse<TranslationEngine>(parts[0], ignoreCase: true, out var engine))
        {
            return;
        }

        var model = parts.Length == 2 ? parts[1] : null;
        var saved = await _changeEngine.ExecuteAsync(engine, model);
        if (saved.IsFailure)
        {
            SetError(saved.Error);
        }
        else
        {
            SetError(null);
            NotifyEngineProperties();
        }
    }

    partial void OnSourceTextChanged(string value) => IsCopied = false;

    partial void OnSelectedSourceChanged(Language value)
    {
        if (!_syncingLanguages && value is not null)
        {
            ApplyLanguages(_languages.WithSource(value));
        }
    }

    partial void OnSelectedTargetChanged(Language value)
    {
        if (!_syncingLanguages && value is not null)
        {
            ApplyLanguages(_languages.WithTarget(value));
        }
    }

    private void ApplyLanguages(Result<LanguagePair> next)
    {
        if (next.IsFailure)
        {
            SetError(next.Error);
            SyncLanguageSelection();
            return;
        }

        if (next.Value == _languages)
        {
            SyncLanguageSelection();
            return;
        }

        _languages = next.Value;
        _detectedSource = null;
        SyncLanguageSelection();
        _ = PersistLanguagesAsync(next.Value);

        RefreshTranslation();
    }

    private void RefreshTranslation()
    {
        _pendingTranslation?.Cancel();
        TranslatedText = string.Empty;
        SetInfo(null);
        SetError(null);

        if (CanTranslate())
        {
            TranslateCommand.Execute(null);
        }
    }

    private async Task PersistLanguagesAsync(LanguagePair languages)
    {
        var saved = await _changeLanguages.ExecuteAsync(languages);
        if (saved.IsFailure)
        {
            SetError(saved.Error);
        }
    }

    private void SyncLanguageSelection()
    {
        _syncingLanguages = true;
        try
        {
            SelectedSource = _languages.Source;
            SelectedTarget = _languages.Target;
        }
        finally
        {
            _syncingLanguages = false;
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        if (_uiLanguage != settings.UiLanguage)
        {
            _uiLanguage = settings.UiLanguage;
            if (_lastError is not null) SetError(_lastError);
            SetInfo(_infoKey, _infoParameter);
        }

        OnPropertyChanged(nameof(HotkeyText));
        OnPropertyChanged(nameof(CompactHotkeyText));
        OnPropertyChanged(nameof(HideOnDeactivate));
        NotifyEngineProperties();

        if (settings.Languages != _languages)
        {
            _languages = settings.Languages;
            _detectedSource = null;
            SyncLanguageSelection();
            RefreshTranslation();
        }
    }

    private void NotifyEngineProperties()
    {
        OnPropertyChanged(nameof(ActiveEngine));
        OnPropertyChanged(nameof(ActiveEngineName));
        OnPropertyChanged(nameof(ActiveModelName));
        OnPropertyChanged(nameof(ActiveEngineSummary));
        OnPropertyChanged(nameof(IsClaudeSonnetActive));
        OnPropertyChanged(nameof(IsClaudeOpusActive));
        OnPropertyChanged(nameof(IsClaudeHaikuActive));
        OnPropertyChanged(nameof(IsCodexActive));
    }

    private void SetError(Error? error)
    {
        _lastError = error;
        ErrorMessage = error is null ? null : UiText.Error(error, _uiLanguage);
    }

    private void SetInfo(string? key, string? parameter = null)
    {
        _infoKey = key;
        _infoParameter = parameter;
        if (key == "DetectedLanguage" && LanguageCatalog.TryFind(parameter) is { } language)
            parameter = UiText.LanguageName(language, _uiLanguage);
        InfoMessage = key is null ? null : UiText.Format(key, _uiLanguage, parameter);
    }
}
