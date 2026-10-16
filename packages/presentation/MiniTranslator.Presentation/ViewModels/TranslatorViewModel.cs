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
        IClipboard clipboard)
    {
        _translate = translate;
        _changeLanguages = changeLanguages;
        _settings = settings;
        _clipboard = clipboard;

        _languages = settings.Current.Languages;
        _uiLanguage = settings.Current.UiLanguage;
        _selectedSource = _languages.Source;
        _selectedTarget = _languages.Target;

        settings.Changed += OnSettingsChanged;
    }

    /// <summary>Raised when the user asks for the settings window.</summary>
    public event EventHandler? SettingsRequested;

    public IReadOnlyList<Language> SourceLanguages => LanguageCatalog.SourceOptions;

    public IReadOnlyList<Language> TargetLanguages => LanguageCatalog.TargetOptions;

    public string HotkeyText => _settings.Current.Hotkey.ToDisplayString();

    /// <summary>Whether the view should hide itself when it loses focus.</summary>
    public bool HideOnDeactivate => _settings.Current.Behavior.HideOnDeactivate;

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

    /// <summary>Called each time the window is shown. When <paramref name="autoTranslate"/> is true, non-empty <paramref name="text"/> is translated immediately.</summary>
    public void Prepare(string? text, bool autoTranslate = true)
    {
        SetError(null);
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
        SetError(null);
        SetInfo(null);

        try
        {
            var result = await _translate.ExecuteAsync(SourceText, _languages, current.Token);
            if (!current.IsCancellationRequested)
            {
                ShowResult(result);
            }
        }
        catch (OperationCanceledException) when (current.IsCancellationRequested)
        {
            // Superseded by a newer request.
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
            SetInfo("Copied");
        }
        else
        {
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
        SetError(null);
        SetInfo(null);
        _detectedSource = null;
    }

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

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
        OnPropertyChanged(nameof(HideOnDeactivate));

        if (settings.Languages != _languages)
        {
            _languages = settings.Languages;
            _detectedSource = null;
            SyncLanguageSelection();
            RefreshTranslation();
        }
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
