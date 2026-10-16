namespace MiniTranslator.Application.Abstractions;

/// <summary>Reads the text currently selected in the foreground application.</summary>
public interface ITextSelectionReader
{
    /// <returns>The selected text, or <c>null</c> when nothing is selected or it cannot be read.</returns>
    Task<string?> ReadSelectionAsync(CancellationToken cancellationToken = default);
}
