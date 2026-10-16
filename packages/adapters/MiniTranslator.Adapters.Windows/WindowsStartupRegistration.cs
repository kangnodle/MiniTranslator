using System.IO;
using System.Security;
using Microsoft.Win32;
using MiniTranslator.Application.Abstractions;
using MiniTranslator.Domain.Common;

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
            // shortcut: Windows can separately block startup apps; revisit if managing that approval becomes required.
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
                if (string.IsNullOrWhiteSpace(_executablePath)
                    || !Path.IsPathFullyQualified(_executablePath)
                    || !File.Exists(_executablePath)
                    || !string.Equals(Path.GetExtension(_executablePath), ".exe", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Path.GetFileName(_executablePath), "dotnet.exe", StringComparison.OrdinalIgnoreCase)
                    || _executablePath.Contains('"')
                    || _executablePath.Length + 2 > 260)
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
}
