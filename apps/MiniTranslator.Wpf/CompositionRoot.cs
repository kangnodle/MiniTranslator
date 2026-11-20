using MiniTranslator.Adapters.ClaudeCli;
using MiniTranslator.Adapters.CodexCli;
using MiniTranslator.Adapters.JsonSettings;
using MiniTranslator.Adapters.Windows;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.Translations;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Wpf.Services;

namespace MiniTranslator.Wpf;

internal sealed class AppContainer : IDisposable
{
    private readonly WindowsDesktopServices _desktop;
    private readonly HotkeyManager _hotkeys;
    private readonly SelectedEngineTranslator _translator;
    private readonly Lazy<ITranslator> _claudeTranslator;
    private readonly Lazy<(ITranslator Translator, ICodexAccount Account)> _codex;
    private readonly AppShell _shell;

    private AppContainer()
    {
        var repository = JsonSettingsAdapter.CreateRepository();
        Settings = new SettingsStore(repository);

        _desktop = WindowsDesktopAdapter.Create();
        _hotkeys = new HotkeyManager(_desktop.Hotkey);

        var codexHome = ResolveCodexHome(AppPaths.Default);
        _claudeTranslator = new Lazy<ITranslator>(() => ClaudeCliAdapter.CreateTranslator(Settings));
        _codex = new Lazy<(ITranslator Translator, ICodexAccount Account)>(() => CodexCliAdapter.Create(Settings, codexHome));

        _translator = new SelectedEngineTranslator(
            Settings,
            engine => engine switch
            {
                TranslationEngine.Codex => _codex.Value.Translator,
                _ => _claudeTranslator.Value,
            });

        var translate = new TranslateTextUseCase(_translator);
        var changeLanguages = new ChangeLanguagesUseCase(Settings);
        var changeEngine = new ChangeEngineUseCase(Settings);
        var captureSelection = new CaptureSelectionUseCase(_desktop.SelectionReader, Settings);

        var translatorViewModel = new TranslatorViewModel(
            translate,
            changeLanguages,
            Settings,
            _desktop.Clipboard,
            changeEngine);

        _shell = new AppShell(
            CreateSettingsViewModel,
            Settings,
            _hotkeys,
            captureSelection,
            translatorViewModel);
    }

    public SettingsStore Settings { get; }

    public AppShell Shell => _shell;

    public static AppContainer Build() => new();

    public void Dispose()
    {
        _shell.Dispose();
        _translator.Dispose();
        if (_claudeTranslator.IsValueCreated && _claudeTranslator.Value is IDisposable claude)
        {
            claude.Dispose();
        }

        if (_codex.IsValueCreated && _codex.Value.Translator is IDisposable codex)
        {
            codex.Dispose();
        }

        _hotkeys.Dispose();
        _desktop.Dispose();
    }

    private SettingsViewModel CreateSettingsViewModel() =>
        new(new SettingsEditor(Settings, _hotkeys, _desktop.Startup, _codex.Value.Account));

    private static string ResolveCodexHome(AppPaths paths)
    {
        var target = paths.CodexHome;
        var legacy = paths.LegacyCodexHome;

        if (!string.Equals(target, legacy, StringComparison.OrdinalIgnoreCase)
            && !System.IO.Directory.Exists(target)
            && System.IO.Directory.Exists(legacy))
        {
            try
            {
                if ((System.IO.File.GetAttributes(legacy) & System.IO.FileAttributes.ReparsePoint) == 0)
                {
                    var parent = System.IO.Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        System.IO.Directory.CreateDirectory(parent);
                    }

                    System.IO.Directory.Move(legacy, target);
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            {
            }
        }

        return target;
    }
}
