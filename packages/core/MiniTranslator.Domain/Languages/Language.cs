namespace MiniTranslator.Domain.Languages;

/// <summary>A language supported by the translator, identified by its BCP-47 code.</summary>
/// <param name="Code">BCP-47 code understood by the translation provider (e.g. "en", "zh-CN").</param>
/// <param name="EnglishName">Name of the language in English.</param>
/// <param name="NativeName">Name of the language in that language.</param>
public sealed record Language(string Code, string EnglishName, string NativeName)
{
    public const string AutoDetectCode = "auto";

    public bool IsAutoDetect => Code == AutoDetectCode;

    public string DisplayName =>
        IsAutoDetect || NativeName == EnglishName ? NativeName : $"{NativeName} ({EnglishName})";

    public override string ToString() => DisplayName;
}
