using MiniTranslator.Domain.Languages;

namespace MiniTranslator.Domain.Tests;

public class LanguagePairTests
{
    private static Language Japanese => LanguageCatalog.FromCode("ja").Value;

    [Fact]
    public void Default_is_english_to_korean()
    {
        Assert.Equal("en", LanguagePair.Default.Source.Code);
        Assert.Equal("ko", LanguagePair.Default.Target.Code);
    }

    [Fact]
    public void Target_cannot_be_auto_detect()
    {
        var result = LanguagePair.Create(LanguageCatalog.English, LanguageCatalog.AutoDetect);
        Assert.Equal(LanguageErrors.TargetCannotBeAutoDetect, result.Error);
    }

    [Fact]
    public void Source_and_target_must_differ()
    {
        var result = LanguagePair.Create(LanguageCatalog.Korean, LanguageCatalog.Korean);
        Assert.Equal(LanguageErrors.SameSourceAndTarget, result.Error);
    }

    [Fact]
    public void Create_from_codes_rejects_unknown_codes()
    {
        Assert.Equal("Language.Unknown", LanguagePair.Create("xx", "ko").Error.Code);
    }

    [Fact]
    public void Swap_inverts_the_pair()
    {
        var swapped = LanguagePair.Default.Swap().Value;

        Assert.Equal("ko", swapped.Source.Code);
        Assert.Equal("en", swapped.Target.Code);
    }

    [Fact]
    public void Swap_fails_for_auto_detect_without_a_detected_language()
    {
        var pair = LanguagePair.Create(LanguageCatalog.AutoDetect, LanguageCatalog.Korean).Value;

        Assert.Equal(LanguageErrors.CannotSwapAutoDetect, pair.Swap().Error);
        Assert.Equal(LanguageErrors.CannotSwapAutoDetect, pair.Swap(detectedSource: null).Error);
    }

    [Fact]
    public void Swap_uses_the_detected_language_when_source_is_auto_detect()
    {
        var pair = LanguagePair.Create(LanguageCatalog.AutoDetect, LanguageCatalog.Korean).Value;

        var swapped = pair.Swap(Japanese).Value;

        Assert.Equal("ko", swapped.Source.Code);
        Assert.Equal("ja", swapped.Target.Code);
    }

    [Fact]
    public void WithSource_changes_only_the_source()
    {
        var pair = LanguagePair.Default.WithSource(Japanese).Value;

        Assert.Equal("ja", pair.Source.Code);
        Assert.Equal("ko", pair.Target.Code);
    }

    [Fact]
    public void WithSource_set_to_the_target_swaps_the_pair()
    {
        var pair = LanguagePair.Default.WithSource(LanguageCatalog.Korean).Value;

        Assert.Equal("ko", pair.Source.Code);
        Assert.Equal("en", pair.Target.Code);
    }

    [Fact]
    public void WithSource_set_to_the_target_fails_when_swapping_is_impossible()
    {
        var pair = LanguagePair.Create(LanguageCatalog.AutoDetect, LanguageCatalog.Korean).Value;

        Assert.Equal(LanguageErrors.SameSourceAndTarget, pair.WithSource(LanguageCatalog.Korean).Error);
    }

    [Fact]
    public void WithTarget_set_to_the_source_swaps_the_pair()
    {
        var pair = LanguagePair.Default.WithTarget(LanguageCatalog.English).Value;

        Assert.Equal("ko", pair.Source.Code);
        Assert.Equal("en", pair.Target.Code);
    }

    [Fact]
    public void WithTarget_rejects_auto_detect()
    {
        Assert.Equal(LanguageErrors.TargetCannotBeAutoDetect, LanguagePair.Default.WithTarget(LanguageCatalog.AutoDetect).Error);
    }
}
