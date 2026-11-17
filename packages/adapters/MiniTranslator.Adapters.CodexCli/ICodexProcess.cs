namespace MiniTranslator.Adapters.CodexCli;

/// <summary>A running Codex app-server, reduced to line-based I/O. Disposing it ends the process.</summary>
internal interface ICodexProcess : IDisposable
{
    /// <exception cref="IOException">The process is gone.</exception>
    Task WriteLineAsync(string line, CancellationToken cancellationToken);

    /// <returns>The next output line, or <c>null</c> once the process has exited.</returns>
    Task<string?> ReadLineAsync(CancellationToken cancellationToken);
}
