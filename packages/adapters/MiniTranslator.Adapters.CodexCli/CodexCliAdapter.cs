using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;

namespace MiniTranslator.Adapters.CodexCli;

public static class CodexCliAdapter
{
    public static (ITranslator Translator, ICodexAccount Account) Create(SettingsStore settings, string home)
    {
        var translator = new CodexCliTranslator(
            () => CodexProcess.Start(CodexCliProtocol.Executables(settings.Current.CodexExecutablePath), home),
            TimeSpan.FromSeconds(90),
            () => settings.Current.Codex,
            CodexCliTranslator.DefaultIdleTimeout);

        var account = new CodexCliAccount(
            executablePath => CodexProcess.Start(CodexCliProtocol.Executables(executablePath), home),
            CodexProcess.OpenBrowser,
            translator.ResetAsync,
            callTimeout: TimeSpan.FromSeconds(30),
            signInTimeout: TimeSpan.FromMinutes(5));

        return (translator, account);
    }
}
