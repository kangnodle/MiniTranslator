using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Abstractions;

namespace MiniTranslator.Adapters.GoogleTranslate;

public static class ServiceCollectionExtensions
{
    /// <summary>Provides <see cref="ITranslator"/> backed by Google Translate's public endpoint.</summary>
    public static IServiceCollection AddGoogleTranslator(this IServiceCollection services)
    {
        // The HttpClient is owned by this adapter, so no other package can pick it up by accident.
        services.AddSingleton<ITranslator>(_ =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            return new GoogleTranslator(http);
        });
        return services;
    }
}
