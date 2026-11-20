using MiniTranslator.Application.Abstractions;

namespace MiniTranslator.Adapters.JsonSettings;

public static class JsonSettingsAdapter
{
    public static ISettingsRepository CreateRepository(AppPaths? paths = null) =>
        new JsonSettingsRepository(paths ?? AppPaths.Default);
}
