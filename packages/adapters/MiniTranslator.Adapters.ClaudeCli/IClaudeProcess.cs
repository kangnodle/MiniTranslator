namespace MiniTranslator.Adapters.ClaudeCli;

internal interface IClaudeProcess : IDisposable
{
    Task WriteLineAsync(string line, CancellationToken cancellationToken);

    Task<string?> ReadLineAsync(CancellationToken cancellationToken);
}
