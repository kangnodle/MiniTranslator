using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Application.Settings;

public sealed record SettingsSaveResult(
    Result<AppSettings> Settings, Error? StartupRollbackError = null, Error? HotkeyRollbackError = null);

/// <summary>One editing session owns settings persistence and desktop effects, including rollback and retry state.</summary>
public sealed class SettingsEditor
{
    private readonly SettingsStore _settings;
    private readonly HotkeyManager _hotkeys;
    private readonly IStartupRegistration _startup;
    private bool _startWithWindows;

    public SettingsEditor(SettingsStore settings, HotkeyManager hotkeys, IStartupRegistration startup)
    {
        _settings = settings;
        _hotkeys = hotkeys;
        _startup = startup;
        Original = settings.Current;
        StartupState = startup.ReadEnabled();
        _startWithWindows = StartupState.GetValueOrDefault(false);
    }

    public AppSettings Original { get; }
    public AppSettings Current => _settings.Current;
    public Result<bool> StartupState { get; }

    public void BeginHotkeyRecording() => _hotkeys.Suspend();
    public Result<Unit> EndHotkeyRecording() => _hotkeys.Resume();

    public Task<Result<AppSettings>> SetThemeAsync(AppTheme theme, CancellationToken cancellationToken = default)
    {
        var next = Current.WithTheme(theme);
        return next.IsFailure
            ? Task.FromResult(next)
            : _settings.UpdateAsync(current => current.WithTheme(theme).Value, cancellationToken);
    }

    public async Task<SettingsSaveResult> SaveAsync(
        AppSettings draft, bool startWithWindows, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var next = Current.MergeDraft(draft, Original);
        if (next.IsFailure)
        {
            return new(next);
        }

        var previousHotkey = Current.Hotkey;
        var startupChanged = StartupState.IsSuccess && startWithWindows != _startWithWindows;
        if (startupChanged)
        {
            var applied = _startup.SetEnabled(startWithWindows);
            if (applied.IsFailure)
            {
                return new(Result<AppSettings>.Failure(applied.Error));
            }
        }

        var committed = false;
        var hotkeyApplied = false;
        Error? startupRollbackError = null;
        Error? hotkeyRollbackError = null;
        Result<AppSettings> saved;
        try
        {
            var applied = _hotkeys.Apply(next.Value.Hotkey);
            hotkeyApplied = applied.IsSuccess;
            saved = applied.IsFailure
                ? Result<AppSettings>.Failure(applied.Error)
                : await _settings.UpdateAsync(current => current.MergeDraft(draft, Original).Value, cancellationToken);
            committed = saved.IsSuccess;
        }
        finally
        {
            if (!committed)
            {
                if (hotkeyApplied)
                {
                    var restored = _hotkeys.Apply(previousHotkey);
                    if (restored.IsFailure) hotkeyRollbackError = restored.Error;
                }

                if (startupChanged)
                {
                    var restored = _startup.SetEnabled(_startWithWindows);
                    if (restored.IsFailure)
                    {
                        _startWithWindows = startWithWindows;
                        startupRollbackError = restored.Error;
                    }
                }
            }
            else if (startupChanged)
            {
                _startWithWindows = startWithWindows;
            }
        }

        return new(saved, startupRollbackError, hotkeyRollbackError);
    }
}
