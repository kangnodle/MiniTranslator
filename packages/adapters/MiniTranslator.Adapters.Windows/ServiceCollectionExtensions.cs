using Microsoft.Extensions.DependencyInjection;
using MiniTranslator.Application.Abstractions;

namespace MiniTranslator.Adapters.Windows;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsDesktop(this IServiceCollection services)
    {
        services.AddSingleton<IGlobalHotkey, Win32GlobalHotkey>();
        services.AddSingleton<IStartupRegistration, WindowsStartupRegistration>();

        services.AddSingleton<WpfClipboard>();
        services.AddSingleton<IClipboard>(sp => sp.GetRequiredService<WpfClipboard>());
        services.AddSingleton<ITextSelectionReader, ClipboardSelectionReader>();

        return services;
    }
}
