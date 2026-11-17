using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Languages;

public sealed record LanguagePair
{
    private LanguagePair(Language source, Language target)
    {
        Source = source;
        Target = target;
    }

    public Language Source { get; }

    public Language Target { get; }

    public static LanguagePair Default { get; } = new(LanguageCatalog.English, LanguageCatalog.Korean);

    public static Result<LanguagePair> Create(Language source, Language target)
    {
        if (target.IsAutoDetect)
        {
            return LanguageErrors.TargetCannotBeAutoDetect;
        }

        if (SameLanguage(source, target))
        {
            return LanguageErrors.SameSourceAndTarget;
        }

        return new LanguagePair(source, target);
    }

    public static Result<LanguagePair> Create(string? sourceCode, string? targetCode) =>
        LanguageCatalog.FromCode(sourceCode)
            .Bind(source => LanguageCatalog.FromCode(targetCode)
                .Bind(target => Create(source, target)));

    public Result<LanguagePair> Swap() =>
        Source.IsAutoDetect ? LanguageErrors.CannotSwapAutoDetect : Create(Target, Source);

    public Result<LanguagePair> Swap(Language? detectedSource) =>
        Source.IsAutoDetect && detectedSource is { IsAutoDetect: false }
            ? Create(Target, detectedSource)
            : Swap();

    public Result<LanguagePair> WithSource(Language source)
    {
        if (!SameLanguage(source, Target))
        {
            return Create(source, Target);
        }

        return Source.IsAutoDetect ? LanguageErrors.SameSourceAndTarget : Create(source, Source);
    }

    public Result<LanguagePair> WithTarget(Language target) =>
        SameLanguage(target, Source) ? Create(Target, target) : Create(Source, target);

    public override string ToString() => $"{Source.Code} → {Target.Code}";

    private static bool SameLanguage(Language a, Language b) =>
        string.Equals(a.Code, b.Code, StringComparison.OrdinalIgnoreCase);
}
