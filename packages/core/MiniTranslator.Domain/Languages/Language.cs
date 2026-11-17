namespace MiniTranslator.Domain.Languages;

public sealed record Language(string Code, string EnglishName, string NativeName)
{
    public const string AutoDetectCode = "auto";

    public bool IsAutoDetect => Code == AutoDetectCode;

    public string DisplayName =>
        IsAutoDetect || NativeName == EnglishName ? NativeName : $"{NativeName} ({EnglishName})";

    public override string ToString() => DisplayName;
}
