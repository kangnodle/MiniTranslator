using System.IO;
using System.Security;
using Microsoft.Win32;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;
using MiniTranslator.Domain.Settings;

namespace MiniTranslator.Adapters.Windows;

internal sealed class WindowsStartupRegistration : IStartupRegistration
{
    private const string ValueName = "MiniTranslator";
    private readonly string? _executablePath;
    private readonly string _runKeyPath;

    public WindowsStartupRegistration()
        : this(Environment.ProcessPath, @"Software\Microsoft\Windows\CurrentVersion\Run") { }

    internal WindowsStartupRegistration(string? executablePath, string runKeyPath)
    {
        _executablePath = executablePath;
        _runKeyPath = runKeyPath;
    }

    public Result<bool> ReadEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
            return key?.GetValue(ValueName) is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return new Error("Startup.ReadFailed", "자동 실행 설정을 읽지 못했습니다.").WithDetail(ex.Message);
        }
    }

    public Result<Unit> SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                if (!IsValidStartupExecutable(_executablePath))
                {
                    return new Error("Startup.InvalidExecutable", "자동 실행을 등록할 수 없습니다. 설치된 MiniTranslator.exe를 실행하고 실행 파일 경로를 258자 이내로 유지하세요.");
                }

                using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath);
                key.SetValue(ValueName, $"\"{_executablePath}\"", RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return Result.Success();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return new Error("Startup.WriteFailed", "자동 실행 설정을 변경하지 못했습니다.").WithDetail(ex.Message);
        }
    }

    internal static bool IsValidStartupExecutable(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath)
            || executablePath.Length + 2 > AppSettings.MaxExecutablePathLength
            || executablePath.Contains('"')
            || !AppSettings.IsValidExecutablePath(executablePath)
            || !File.Exists(executablePath)
            || string.Equals(Path.GetFileName(executablePath), "dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var drive = new DriveInfo(executablePath[..1]);
            if (drive.DriveType == DriveType.Network)
            {
                return false;
            }

            if (File.ResolveLinkTarget(executablePath, returnFinalTarget: true) is { } target)
            {
                var targetPath = target.FullName;
                if (!AppSettings.IsValidExecutablePath(targetPath)
                    || new DriveInfo(targetPath[..1]).DriveType == DriveType.Network)
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return false;
        }
    }
}
