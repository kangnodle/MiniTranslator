namespace MiniTranslator.Application.Abstractions;

public interface ITextSelectionReader
{
    Task<string?> ReadSelectionAsync(CancellationToken cancellationToken = default);
}
