using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.CodexCli;

internal sealed class CodexProcess : ICodexProcess
{
    internal const int MaxLineLength = 2 * 1024 * 1024;

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
        var resolvedExecutable = ResolveExecutable(executable)
            ?? throw new Win32Exception(2, "The Codex CLI executable could not be resolved.");
        var workingDirectory = EnsureSandboxWorkingDirectory();
        Directory.CreateDirectory(home);

        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var startInfo = new ProcessStartInfo(resolvedExecutable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = utf8,
            StandardOutputEncoding = utf8,
            StandardErrorEncoding = utf8,
            WorkingDirectory = workingDirectory,
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

    internal static string? ResolveExecutable(string executable)
    {
        if (string.Equals(executable, CodexCliProtocol.Executable, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveExecutableFromPath(CodexCliProtocol.Executable);
        }

        var normalized = AppSettings.NormalizePath(executable);
        return AppSettings.IsValidExecutablePath(normalized) && File.Exists(normalized) ? normalized : null;
    }

    internal static string? ResolveExecutableFromPath(string name)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        var isWindows = Path.PathSeparator == ';';
        string[] candidates = isWindows ? [$"{name}.exe", $"{name}.com", name] : [name];

        foreach (var rawEntry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var entry = rawEntry.Trim('"');
            if (entry.Length == 0 || !Path.IsPathFullyQualified(entry) || entry.StartsWith(@"\\", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                var fullPath = Path.Combine(entry, candidate);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    internal static string EnsureSandboxWorkingDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MiniTranslator.Work");
        Directory.CreateDirectory(directory);
        return directory;
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
        var line = await ReadBoundedLineAsync(_process.StandardOutput, MaxLineLength);
        cancellationToken.ThrowIfCancellationRequested();
        return line;
    }

    internal static async Task<string?> ReadBoundedLineAsync(TextReader reader, int maxLength)
    {
        var buffer = new StringBuilder();
        var chunk = new char[1];
        var anyRead = false;

        while (true)
        {
            var read = await reader.ReadAsync(chunk, 0, 1);
            if (read == 0)
            {
                return anyRead ? buffer.ToString() : null;
            }

            anyRead = true;
            var ch = chunk[0];
            if (ch == '\n')
            {
                if (buffer.Length > 0 && buffer[^1] == '\r')
                {
                    buffer.Length--;
                }

                return buffer.ToString();
            }

            if (buffer.Length >= maxLength)
            {
                throw new IOException("CLI output line exceeded the maximum allowed length.");
            }

            buffer.Append(ch);
        }
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
