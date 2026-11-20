using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.CodexCli;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCodexCliTranslator(this IServiceCollection services, string home)
    {
        services.AddSingleton(provider =>
        {
            var settings = provider.GetRequiredService<SettingsStore>();
            return new CodexCliTranslator(
                () => CodexProcess.Start(CodexCliProtocol.Executables(settings.Current.CodexExecutablePath), home),
                TimeSpan.FromSeconds(90),
                () => settings.Current.Codex);
        });
        services.AddKeyedSingleton<ITranslator>(TranslationEngine.Codex, (provider, _) => provider.GetRequiredService<CodexCliTranslator>());

        services.AddSingleton<ICodexAccount>(provider => new CodexCliAccount(
            executablePath => CodexProcess.Start(CodexCliProtocol.Executables(executablePath), home),
            CodexProcess.OpenBrowser,
            provider.GetRequiredService<CodexCliTranslator>().ResetAsync,
            callTimeout: TimeSpan.FromSeconds(30),
            signInTimeout: TimeSpan.FromMinutes(5)));
        return services;
    }
}
