using MiniTranslator.Domain.Common;

namespace MiniTranslator.Domain.Languages;

/// <summary>
/// An always-valid source/target language combination.
/// Instances can only be created through <see cref="Create(Language, Language)"/>, which enforces the invariants.
/// </summary>
public sealed record LanguagePair
{
    private LanguagePair(Language source, Language target)
    {
        Source = source;
        Target = target;
    }

    public Language Source { get; }

    public Language Target { get; }

    /// <summary>Default pair: English → Korean.</summary>
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

    /// <summary>
    /// Swaps the pair. When the source is auto-detect, the language the provider detected
    /// (if known) becomes the new target.
    /// </summary>
    public Result<LanguagePair> Swap(Language? detectedSource) =>
        Source.IsAutoDetect && detectedSource is { IsAutoDetect: false }
            ? Create(Target, detectedSource)
            : Swap();

    /// <summary>
    /// Changes the source language. Picking the current target swaps the pair,
    /// the way most translators behave.
    /// </summary>
    public Result<LanguagePair> WithSource(Language source)
    {
        if (!SameLanguage(source, Target))
        {
            return Create(source, Target);
        }

        return Source.IsAutoDetect ? LanguageErrors.SameSourceAndTarget : Create(source, Source);
    }

    /// <summary>
    /// Changes the target language. Picking the current source swaps the pair.
    /// </summary>
    public Result<LanguagePair> WithTarget(Language target) =>
        SameLanguage(target, Source) ? Create(Target, target) : Create(Source, target);

    public override string ToString() => $"{Source.Code} → {Target.Code}";

    private static bool SameLanguage(Language a, Language b) =>
        string.Equals(a.Code, b.Code, StringComparison.OrdinalIgnoreCase);
}
