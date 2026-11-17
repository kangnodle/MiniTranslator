using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace MiniTranslator.Adapters.ClaudeCli;

/// <summary>Thin shell over the <c>claude</c> child process. It closes with this app because its stdin closes.</summary>
internal sealed class ClaudeProcess : IClaudeProcess
{
    private readonly Process _process;

    private ClaudeProcess(Process process) => _process = process;

    /// <exception cref="Win32Exception">The CLI is not installed or cannot be started.</exception>
    public static ClaudeProcess Start()
    {
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var startInfo = new ProcessStartInfo(ClaudeCliProtocol.Executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = utf8,
            StandardOutputEncoding = utf8,
            StandardErrorEncoding = utf8,
            // Away from any project folder, so no repository instructions are picked up.
            WorkingDirectory = Path.GetTempPath(),
        };

        foreach (var argument in ClaudeCliProtocol.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo) ?? throw new Win32Exception("The Claude Code CLI did not start.");

        // Drain stderr so the CLI can never block on a full pipe.
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginErrorReadLine();

        return new ClaudeProcess(process);
    }

    public async Task WriteLineAsync(string line, CancellationToken cancellationToken)
    {
        await _process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken);
        await _process.StandardInput.FlushAsync(cancellationToken);
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        // A pipe read cannot be cancelled reliably; ending the process is what unblocks it.
        await using var kill = cancellationToken.Register(Kill);
        var line = await _process.StandardOutput.ReadLineAsync(CancellationToken.None);
        cancellationToken.ThrowIfCancellationRequested();
        return line;
    }

    public void Dispose()
    {
        Kill();
        _process.Dispose();
    }

    private void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already gone, or going: there is nothing left to stop.
        }
    }
}
