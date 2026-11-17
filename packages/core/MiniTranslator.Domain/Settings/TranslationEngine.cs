namespace MiniTranslator.Domain.Settings;

/// <summary>Which provider performs translations.</summary>
public enum TranslationEngine
{
    /// <summary>Google Translate's public endpoint: fast, no sign-in.</summary>
    Google,

    /// <summary>The locally installed Claude Code CLI, using its own sign-in.</summary>
    Claude,

    /// <summary>The locally installed Codex CLI, using its own ChatGPT sign-in.</summary>
    Codex,
}
