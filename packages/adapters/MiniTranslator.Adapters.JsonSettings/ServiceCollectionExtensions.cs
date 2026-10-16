using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Abstractions;

namespace MiniTranslator.Adapters.JsonSettings;

public static class ServiceCollectionExtensions
{
    /// <summary>Provides <see cref="ISettingsRepository"/> backed by a JSON file under <paramref name="paths"/>.</summary>
    public static IServiceCollection AddJsonSettings(this IServiceCollection services, AppPaths? paths = null)
    {
        services.AddSingleton(paths ?? AppPaths.Default);
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
        return services;
    }
}
