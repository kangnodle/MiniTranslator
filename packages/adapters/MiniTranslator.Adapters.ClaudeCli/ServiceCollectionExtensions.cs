using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.ClaudeCli;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddClaudeCliTranslator(this IServiceCollection services)
    {
        services.AddKeyedSingleton<ITranslator>(
            TranslationEngine.Claude,
            (_, _) => new ClaudeCliTranslator(ClaudeProcess.Start, TimeSpan.FromSeconds(90)));
        return services;
    }
}
