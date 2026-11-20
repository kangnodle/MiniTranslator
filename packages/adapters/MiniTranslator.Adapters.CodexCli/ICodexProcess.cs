namespace MiniTranslator.Adapters.CodexCli;

internal interface ICodexProcess : IDisposable
{
    string WorkingDirectory => CodexProcess.EnsureSandboxWorkingDirectory();

    Task WriteLineAsync(string line, CancellationToken cancellationToken);

    Task<string?> ReadLineAsync(CancellationToken cancellationToken);
}
