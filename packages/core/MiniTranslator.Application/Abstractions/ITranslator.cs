using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Translations;

namespace MiniTranslator.Application.Abstractions;

public interface ITranslator
{
    Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TranslationResult>> TranslateAsync(
        TranslationRequest request,
        IProgress<string>? onPartialText,
        CancellationToken cancellationToken = default) =>
        TranslateAsync(request, cancellationToken);
}
