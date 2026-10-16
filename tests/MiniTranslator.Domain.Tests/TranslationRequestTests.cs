using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Domain.Tests;

public class TranslationRequestTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n\t")]
    public void Rejects_blank_text(string? text)
    {
        Assert.Equal(TranslationErrors.EmptyText, TranslationRequest.Create(text, LanguagePair.Default).Error);
    }

    [Fact]
    public void Trims_text()
    {
        var request = TranslationRequest.Create("  hello world \n", LanguagePair.Default).Value;
        Assert.Equal("hello world", request.Text);
    }

    [Fact]
    public void Accepts_text_at_the_limit_and_rejects_longer()
    {
        Assert.True(TranslationRequest.Create(new string('a', TranslationRequest.MaxLength), LanguagePair.Default).IsSuccess);
        Assert.Equal("Translation.TooLong", TranslationRequest.Create(new string('a', TranslationRequest.MaxLength + 1), LanguagePair.Default).Error.Code);
    }

    [Fact]
    public void Result_describes_the_detected_language()
    {
        var result = new TranslationResult("Bonjour", "안녕하세요", LanguagePair.Default, "fr");
        Assert.Equal("Français (French)", result.DetectedSourceLanguageName);
    }
}
