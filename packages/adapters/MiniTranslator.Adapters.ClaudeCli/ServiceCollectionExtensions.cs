using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.ClaudeCli;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Provides the <see cref="ITranslator"/> keyed <see cref="TranslationEngine.Claude"/>, backed by the Claude Code CLI.
    /// Nothing is started until the first translation.
    /// </summary>
    public static IServiceCollection AddClaudeCliTranslator(this IServiceCollection services)
    {
        // The timeout leaves room for the longest allowed request at the model's output speed.
        services.AddKeyedSingleton<ITranslator>(
            TranslationEngine.Claude,
            (_, _) => new ClaudeCliTranslator(ClaudeProcess.Start, TimeSpan.FromSeconds(90)));
        return services;
    }
}
