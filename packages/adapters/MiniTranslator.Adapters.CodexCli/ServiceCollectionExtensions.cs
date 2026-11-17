using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.CodexCli;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Provides the <see cref="ITranslator"/> keyed <see cref="TranslationEngine.Codex"/> and the
    /// <see cref="ICodexAccount"/> that signs it in, both backed by the Codex CLI.
    /// Nothing is started until the first translation or account request.
    /// </summary>
    /// <param name="home">
    /// The profile folder the CLI signs in from. It belongs to this app, so the user's own Codex settings,
    /// instructions, and history never reach a translation.
    /// </param>
    public static IServiceCollection AddCodexCliTranslator(this IServiceCollection services, string home)
    {
        // The timeout leaves room for the longest allowed request at the model's output speed.
        services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<SettingsStore>();
            return new CodexCliTranslator(
                () => CodexProcess.Start(CodexCliProtocol.Executables(settings.Current.CodexExecutablePath), home),
                TimeSpan.FromSeconds(90));
        });
        services.AddKeyedSingleton<ITranslator>(TranslationEngine.Codex, (provider, _) => provider.GetRequiredService<CodexCliTranslator>());

        // Five minutes is how long the user has to finish signing in in the browser.
        services.AddSingleton<ICodexAccount>(provider => new CodexCliAccount(
            executablePath => CodexProcess.Start(CodexCliProtocol.Executables(executablePath), home),
            CodexProcess.OpenBrowser,
            provider.GetRequiredService<CodexCliTranslator>().ResetAsync,
            callTimeout: TimeSpan.FromSeconds(30),
            signInTimeout: TimeSpan.FromMinutes(5)));
        return services;
    }
}
