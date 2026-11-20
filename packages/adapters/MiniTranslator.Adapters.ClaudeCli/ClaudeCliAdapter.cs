using MiniTranslator.Application.Abstractions;
using MiniTranslator.Application.Settings;

namespace MiniTranslator.Adapters.ClaudeCli;

public static class ClaudeCliAdapter
{
    public static ITranslator CreateTranslator(SettingsStore settings) =>
        new ClaudeCliTranslator(
            ClaudeProcess.Start,
            TimeSpan.FromSeconds(90),
            () => settings.Current.Claude,
            ClaudeCliTranslator.DefaultIdleTimeout);
}
