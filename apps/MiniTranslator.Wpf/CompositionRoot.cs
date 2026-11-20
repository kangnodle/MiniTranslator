using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Adapters.ClaudeCli;
using MiniTranslator.Adapters.CodexCli;
using MiniTranslator.Adapters.JsonSettings;
using MiniTranslator.Adapters.Windows;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Hotkeys;
using MiniTranslator.Application.Settings;
using MiniTranslator.Application.Translations;
using MiniTranslator.Application.UseCases;
using MiniTranslator.Domain.Settings;
using MiniTranslator.Presentation.ViewModels;
using MiniTranslator.Wpf.Services;

namespace MiniTranslator.Wpf;

internal static class CompositionRoot
{
    public static ServiceProvider Build() =>
        new ServiceCollection()
            .AddCore()
            .AddClaudeCliTranslator()
            .AddCodexCliTranslator(System.IO.Path.Combine(AppPaths.Default.Root, "codex"))
            .AddJsonSettings()
            .AddWindowsDesktop()
            .AddPresentation()
            .AddAppShell()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

    private static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<HotkeyManager>();

        services.AddSingleton<ITranslator>(sp => new SelectedEngineTranslator(
            sp.GetRequiredService<SettingsStore>(),
            engine => sp.GetRequiredKeyedService<ITranslator>(engine)));

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

        services.AddSingleton<Func<SettingsViewModel>>(sp => sp.GetRequiredService<SettingsViewModel>);

        return services;
    }

    private static IServiceCollection AddAppShell(this IServiceCollection services)
    {
        services.AddSingleton<AppShell>();
        return services;
    }
}
