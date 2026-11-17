using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace MiniTranslator.Adapters.ClaudeCli;

internal sealed class ClaudeProcess : IClaudeProcess
{
    private readonly Process _process;

    private ClaudeProcess(Process process) => _process = process;

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
            WorkingDirectory = Path.GetTempPath(),
        };

        foreach (var argument in ClaudeCliProtocol.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo) ?? throw new Win32Exception("The Claude Code CLI did not start.");

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
        }
    }
}
