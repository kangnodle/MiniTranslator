namespace MiniTranslator.Adapters.ClaudeCli;

/// <summary>A running Claude Code CLI session, reduced to line-based I/O. Disposing it ends the process.</summary>
internal interface IClaudeProcess : IDisposable
{
    /// <exception cref="IOException">The process is gone.</exception>
    Task WriteLineAsync(string line, CancellationToken cancellationToken);

    /// <returns>The next output line, or <c>null</c> once the process has exited.</returns>
    Task<string?> ReadLineAsync(CancellationToken cancellationToken);
}
