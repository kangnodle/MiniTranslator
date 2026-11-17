using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.GoogleTranslate;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGoogleTranslator(this IServiceCollection services)
    {
        services.AddKeyedSingleton<ITranslator>(TranslationEngine.Google, (_, _) =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return new GoogleTranslator(http);
        });
        return services;
    }
}
