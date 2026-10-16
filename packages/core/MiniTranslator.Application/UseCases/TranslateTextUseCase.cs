using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Languages;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Application.UseCases;

public sealed class TranslateTextUseCase(ITranslator translator)
{
    public async Task<Result<TranslationResult>> ExecuteAsync(
        string? text,
        LanguagePair languages,
        CancellationToken cancellationToken = default)
    {
        var request = TranslationRequest.Create(text, languages);
        if (request.IsFailure)
        {
            return request.Error;
        }

        return await translator.TranslateAsync(request.Value, cancellationToken);
    }
}
