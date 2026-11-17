using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace MiniTranslator.Adapters.CodexCli;

/// <summary>Thin shell over the <c>codex app-server</c> child process. It closes with this app because its stdin closes.</summary>
internal sealed class CodexProcess : ICodexProcess
{
    private readonly Process _process;

    private CodexProcess(Process process) => _process = process;

    /// <summary>Starts the first of <paramref name="executables"/> that can be started.</summary>
    /// <param name="executables">Candidates for the Codex CLI: commands found on PATH, or full paths.</param>
    /// <param name="home">The profile folder holding the CLI's sign-in for this app; created when missing.</param>
    /// <exception cref="Win32Exception">None of the candidates is installed or can be started.</exception>
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
            // Away from any project folder, so no repository instructions are picked up.
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

        // Drain stderr so the CLI can never block on a full pipe.
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginErrorReadLine();

        return new CodexProcess(process);
    }

    /// <summary>Opens <paramref name="page"/> in the user's default browser.</summary>
    /// <exception cref="Win32Exception">No program is registered to open it.</exception>
    public static void OpenBrowser(Uri page) =>
        Process.Start(new ProcessStartInfo(page.AbsoluteUri) { UseShellExecute = true })?.Dispose();

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
