using MiniTranslator.Domain.Hotkeys;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Domain.Tests;

public class HotkeyModifiersTests
{
    [Fact]
    public void Names_follow_the_canonical_order()
    {
        var all = HotkeyModifiers.Windows | HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Control;

        Assert.Equal(["Ctrl", "Alt", "Shift", "Win"], all.Names());
    }

    [Theory]
    [InlineData(HotkeyModifiers.None, "…")]
    [InlineData(HotkeyModifiers.Control, "Ctrl + …")]
    [InlineData(HotkeyModifiers.Control | HotkeyModifiers.Alt, "Ctrl + Alt + …")]
    public void Pending_display_shows_held_modifiers(HotkeyModifiers modifiers, string expected)
    {
        Assert.Equal(expected, modifiers.ToPendingDisplayString());
    }
}

public class LanguageCatalogTests
{
    [Fact]
    public void TryFind_is_case_insensitive()
    {
        Assert.Equal("zh-CN", LanguageCatalog.TryFind("ZH-cn")?.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("xx")]
    public void TryFind_returns_null_for_unknown_codes(string? code)
    {
        Assert.Null(LanguageCatalog.TryFind(code));
    }

    [Fact]
    public void DescribeCode_falls_back_to_the_raw_code()
    {
        Assert.Equal("xx", LanguageCatalog.DescribeCode("xx"));
    }
}

public class TranslationResultTests
{
    private static readonly LanguagePair AutoToKorean =
        LanguagePair.Create(LanguageCatalog.AutoDetect, LanguageCatalog.Korean).Value;

    [Fact]
    public void Detected_language_is_resolved_from_the_catalog()
    {
        var result = new TranslationResult("Hola", "안녕", AutoToKorean, "es");

        Assert.Equal("es", result.DetectedSourceLanguage?.Code);
        Assert.Equal("감지된 언어: Español (Spanish)", result.DetectionNote);
    }

    [Fact]
    public void Unknown_detected_language_still_gets_a_note_but_no_language()
    {
        var result = new TranslationResult("?", "?", AutoToKorean, "xx");

        Assert.Null(result.DetectedSourceLanguage);
        Assert.Equal("감지된 언어: xx", result.DetectionNote);
    }

    [Fact]
    public void No_note_when_the_source_was_chosen_explicitly()
    {
        var result = new TranslationResult("Hello", "안녕", LanguagePair.Default, "en");

        Assert.Null(result.DetectionNote);
    }
}
