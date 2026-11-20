using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.ClaudeCli;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClaudeCliTranslator(this IServiceCollection services)
    {
        services.AddKeyedSingleton<ITranslator>(TranslationEngine.Claude, (provider, _) =>
        {
            var settings = provider.GetRequiredService<SettingsStore>();
            return new ClaudeCliTranslator(ClaudeProcess.Start, TimeSpan.FromSeconds(90), () => settings.Current.Claude);
        });
        return services;
    }
}
