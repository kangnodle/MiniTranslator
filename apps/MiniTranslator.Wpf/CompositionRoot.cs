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

        var codexHome = System.IO.Path.Combine(AppPaths.Default.Root, "codex");
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
}
