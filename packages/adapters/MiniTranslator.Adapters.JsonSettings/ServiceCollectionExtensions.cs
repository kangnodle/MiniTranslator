using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Abstractions;

namespace MiniTranslator.Adapters.JsonSettings;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJsonSettings(this IServiceCollection services, AppPaths? paths = null)
    {
        services.AddSingleton(paths ?? AppPaths.Default);
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
        return services;
    }
}
