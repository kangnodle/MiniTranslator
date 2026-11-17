namespace MiniTranslator.Adapters.CodexCli;

internal interface ICodexProcess : IDisposable
{
    Task WriteLineAsync(string line, CancellationToken cancellationToken);

    Task<string?> ReadLineAsync(CancellationToken cancellationToken);
}
