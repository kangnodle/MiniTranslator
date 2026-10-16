using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Adapters.GoogleTranslate;
using MiniTranslator.Adapters.JsonSettings;
using MiniTranslator.Adapters.Windows;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Wpf.Services;

namespace MiniTranslator.Wpf;

/// <summary>
/// The single place that knows every package. Core and Presentation stay free of any DI container;
/// adapters expose one <c>AddXxx</c> extension each because their implementations are internal.
/// </summary>
internal static class CompositionRoot
{
    public static ServiceProvider Build() =>
        new ServiceCollection()
            .AddCore()
            .AddGoogleTranslator()
            .AddJsonSettings()
            .AddWindowsDesktop()
            .AddPresentation()
            .AddAppShell()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    private static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<HotkeyManager>();

        services.AddTransient<TranslateTextUseCase>();
        services.AddTransient<ChangeLanguagesUseCase>();
        services.AddTransient<SettingsEditor>();
        services.AddTransient<CaptureSelectionUseCase>();

        return services;
    }

    private static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddSingleton<TranslatorViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Each settings window gets a fresh draft; the shell asks for one through this factory instead of the container.
        services.AddSingleton<Func<SettingsViewModel>>(sp => sp.GetRequiredService<SettingsViewModel>);

        return services;
    }

    private static IServiceCollection AddAppShell(this IServiceCollection services)
    {
        services.AddSingleton<AppShell>();
        return services;
    }
}
