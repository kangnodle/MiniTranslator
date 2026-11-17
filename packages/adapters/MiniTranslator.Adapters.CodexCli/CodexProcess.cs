using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace MiniTranslator.Adapters.CodexCli;

internal sealed class CodexProcess : ICodexProcess
{
    private readonly Process _process;

    private CodexProcess(Process process) => _process = process;

    public static CodexProcess Start(IReadOnlyList<string> executables, string home)
    {
        Win32Exception? failure = null;
        foreach (var executable in executables)
        {
            try
            {
                return Start(executable, home);
            }
            catch (Win32Exception ex)
            {
                failure = ex;
            }
        }

        throw failure ?? new Win32Exception("No Codex CLI was given to start.");
    }

    private static CodexProcess Start(string executable, string home)
    {
        Directory.CreateDirectory(home);

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var startInfo = new ProcessStartInfo(executable)
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

        foreach (var argument in CodexCliProtocol.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var variable in startInfo.Environment.Keys.Where(CodexCliProtocol.ReplacesSignIn).ToList())
        {
            startInfo.Environment.Remove(variable);
        }

        startInfo.Environment[CodexCliProtocol.HomeVariable] = home;

        var process = Process.Start(startInfo) ?? throw new Win32Exception("The Codex CLI did not start.");

        process.ErrorDataReceived += static (_, _) => { };
        process.BeginErrorReadLine();

        return new CodexProcess(process);
    }

    public static void OpenBrowser(Uri page) =>
        Process.Start(new ProcessStartInfo(page.AbsoluteUri) { UseShellExecute = true })?.Dispose();

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
