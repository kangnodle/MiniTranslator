using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.ClaudeCli;

internal sealed class ClaudeProcess : IClaudeProcess
{
    internal const int MaxLineLength = 2 * 1024 * 1024;

    private readonly Process _process;

    private ClaudeProcess(Process process) => _process = process;

    public static ClaudeProcess Start(EngineOptions options)
    {
        var executable = ResolveExecutableFromPath(ClaudeCliProtocol.Executable)
            ?? throw new Win32Exception(2, "The Claude Code CLI was not found on PATH.");
        var workingDirectory = EnsureSandboxWorkingDirectory();

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
            WorkingDirectory = workingDirectory,
        };

        foreach (var argument in ClaudeCliProtocol.Arguments(options))
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = Process.Start(startInfo) ?? throw new Win32Exception("The Claude Code CLI did not start.");

        process.ErrorDataReceived += static (_, _) => { };
        process.BeginErrorReadLine();

        return new ClaudeProcess(process);
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
