using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.CodexCli;

internal sealed class CodexProcess : ICodexProcess
{
    internal const int MaxLineLength = 2 * 1024 * 1024;

    private static readonly HashSet<string> AllowedEnvironmentVariables = new(StringComparer.OrdinalIgnoreCase)
    {
        "SystemRoot",
        "WINDIR",
        "SystemDrive",
        "COMSPEC",
        "PATHEXT",
        "PATH",
        "ProgramFiles",
        "ProgramFiles(x86)",
        "ProgramW6432",
        "CommonProgramFiles",
        "CommonProgramFiles(x86)",
        "CommonProgramW6432",
        "ProgramData",
        "USERPROFILE",
        "HOME",
        "HOMEDRIVE",
        "HOMEPATH",
        "USERNAME",
        "USERDOMAIN",
        "APPDATA",
        "LOCALAPPDATA",
        "PUBLIC",
        "TEMP",
        "TMP",
        "NUMBER_OF_PROCESSORS",
        "PROCESSOR_ARCHITECTURE",
        "PROCESSOR_IDENTIFIER",
        "OS",
        "LANG",
        "LC_ALL",
        "LC_CTYPE",
        "TERM",
    };

    private readonly Process _process;
    private readonly string _sessionWorkingDirectory;

    private CodexProcess(Process process, string sessionWorkingDirectory)
    {
        _process = process;
        _sessionWorkingDirectory = sessionWorkingDirectory;
    }

    public string WorkingDirectory => _sessionWorkingDirectory;

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
        EnsureSafeHomeDirectory(home);
        var workingDirectory = CreateSessionWorkingDirectory();

        try
        {
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

            SanitizeEnvironment(startInfo.Environment);
            startInfo.Environment[CodexCliProtocol.HomeVariable] = home;

            var process = Process.Start(startInfo) ?? throw new Win32Exception("The Codex CLI did not start.");

            _ = DrainStreamAsync(process.StandardError.BaseStream);

            return new CodexProcess(process, workingDirectory);
        }
        catch
        {
            TryDeleteSessionDirectory(workingDirectory);
            throw;
        }
    }

    internal static bool IsAllowedEnvironmentVariable(string name) =>
        AllowedEnvironmentVariables.Contains(name) && !CodexCliProtocol.ReplacesSignIn(name);

    internal static void SanitizeEnvironment(IDictionary<string, string?> environment)
    {
        foreach (var key in environment.Keys.Where(key => !IsAllowedEnvironmentVariable(key)).ToList())
        {
            environment.Remove(key);
        }
    }

    internal static string? ResolveExecutable(string executable)
    {
        if (string.Equals(executable, CodexCliProtocol.Executable, StringComparison.OrdinalIgnoreCase))
        {
            return ResolveExecutableFromPath(CodexCliProtocol.Executable);
        }

        var normalized = AppSettings.NormalizePath(executable);
        return AppSettings.IsValidExecutablePath(normalized)
            && IsSafeLocalExecutable(normalized, isWindows: Path.PathSeparator == ';', requireExeOnly: true)
            ? normalized
            : null;
    }

    internal static string? ResolveExecutableFromPath(string name) =>
        ResolveExecutableFromPath(name, Environment.GetEnvironmentVariable("PATH"), Path.PathSeparator == ';');

    internal static string? ResolveExecutableFromPath(string name, string? pathValue, bool isWindows)
    {
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return null;
        }

        string[] candidates = isWindows ? [$"{name}.exe", $"{name}.com"] : [name];

        foreach (var rawEntry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var entry = rawEntry.Trim('"');
            if (!IsSafePathDirectory(entry, isWindows))
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                var fullPath = Path.Combine(entry, candidate);
                if (IsSafeLocalExecutable(fullPath, isWindows, requireExeOnly: false))
                {
                    return fullPath;
                }
            }
        }

        return null;
    }

    internal static bool IsSafePathDirectory(string entry, bool isWindows)
    {
        if (entry.Length == 0 || !Path.IsPathFullyQualified(entry))
        {
            return false;
        }

        if (entry.Length >= 2 && IsDirectorySeparator(entry[0]) && IsDirectorySeparator(entry[1]))
        {
            return false;
        }

        if (!isWindows)
        {
            return true;
        }

        if (entry.Length < 3
            || !char.IsAsciiLetter(entry[0])
            || entry[1] != ':'
            || !IsDirectorySeparator(entry[2])
            || entry.AsSpan(2).Contains(':'))
        {
            return false;
        }

        return IsLocalDrive(entry);
    }

    internal static bool IsSafeLocalExecutable(string fullPath, bool isWindows, bool requireExeOnly)
    {
        if (!IsSafePathDirectory(fullPath, isWindows) || !File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            if (File.ResolveLinkTarget(fullPath, returnFinalTarget: true) is { } target)
            {
                var targetPath = target.FullName;
                if (!IsSafePathDirectory(targetPath, isWindows))
                {
                    return false;
                }

                if (isWindows && !HasAllowedWindowsExtension(targetPath, requireExeOnly))
                {
                    return false;
                }
            }

            return AreParentDirectoriesSafe(fullPath, isWindows);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }

    internal static string EnsureSandboxWorkingDirectory()
    {
        var baseRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseRoot))
        {
            baseRoot = Path.GetTempPath();
        }

        var appRoot = Path.Combine(baseRoot, "MiniTranslator");
        EnsureNonReparsePointDirectory(appRoot);

        var directory = Path.Combine(appRoot, "work");
        EnsureNonReparsePointDirectory(directory);
        return directory;
    }

    internal static string CreateSessionWorkingDirectory()
    {
        var root = EnsureSandboxWorkingDirectory();
        var sessionDirectory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        EnsureNonReparsePointDirectory(sessionDirectory);
        return sessionDirectory;
    }

    internal static void EnsureSafeHomeDirectory(string home)
    {
        var parent = Path.GetDirectoryName(home);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureNonReparsePointDirectory(parent);
        }

        EnsureNonReparsePointDirectory(home);
    }

    private static void EnsureNonReparsePointDirectory(string path)
    {
        if (Directory.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Directory cannot be a reparse point.");
        }

        Directory.CreateDirectory(path);

        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("Directory cannot be a reparse point.");
        }
    }

    private static bool AreParentDirectoriesSafe(string fullPath, bool isWindows)
    {
        var current = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                if (Directory.ResolveLinkTarget(current, returnFinalTarget: true) is not { } target
                    || !IsSafePathDirectory(target.FullName, isWindows))
                {
                    return false;
                }
            }

            var parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }

        return true;
    }

    private static bool IsLocalDrive(string path)
    {
        try
        {
            var drive = new DriveInfo(path[..1]);
            return drive.DriveType != DriveType.Network;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            return false;
        }
    }

    private static bool HasAllowedWindowsExtension(string path, bool requireExeOnly) =>
        path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
        || (!requireExeOnly && path.EndsWith(".com", StringComparison.OrdinalIgnoreCase));

    private static bool IsDirectorySeparator(char ch) => ch is '\\' or '/';

    private static async Task DrainStreamAsync(Stream stream)
    {
        var buffer = new byte[4096];
        try
        {
            while (await stream.ReadAsync(buffer).ConfigureAwait(false) > 0)
            {
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
        }
    }

    private static void TryDeleteSessionDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0)
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
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
        TryDeleteSessionDirectory(_sessionWorkingDirectory);
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
