using Microsoft.Win32;
using MiniTranslator.Adapters.Windows;

namespace MiniTranslator.Adapters.Windows.Tests;

public sealed class StartupRegistrationTests : IDisposable
{
    private readonly string _keyPath = @"Software\MiniTranslator.Tests\" + Guid.NewGuid();
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "MiniTranslator startup tests " + Guid.NewGuid());

    [Fact]
    public void Registration_quotes_the_executable_and_removes_only_its_own_value()
    {
        Directory.CreateDirectory(_directory);
        var executable = Path.Combine(_directory, "MiniTranslator.exe");
        File.WriteAllText(executable, "");
        var startup = new WindowsStartupRegistration(executable, _keyPath);
        Assert.False(startup.ReadEnabled().Value);

        Assert.True(startup.SetEnabled(true).IsSuccess);
        Assert.True(startup.ReadEnabled().Value);
        using (var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true))
        {
            Assert.Equal($"\"{executable}\"", key!.GetValue("MiniTranslator"));
            Assert.Equal(RegistryValueKind.String, key.GetValueKind("MiniTranslator"));
            key.SetValue("OtherApp", "preserve");
        }

        Assert.True(startup.SetEnabled(false).IsSuccess);
        Assert.True(startup.SetEnabled(false).IsSuccess);
        Assert.False(startup.ReadEnabled().Value);
        using var remaining = Registry.CurrentUser.OpenSubKey(_keyPath);
        Assert.Equal("preserve", remaining!.GetValue("OtherApp"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("MiniTranslator.exe")]
    [InlineData(@"C:\missing\MiniTranslator.exe")]
    public void Invalid_executable_does_not_create_a_registration(string? executable)
    {
        var startup = new WindowsStartupRegistration(executable, _keyPath);

        Assert.Equal("Startup.InvalidExecutable", startup.SetEnabled(true).Error.Code);
        Assert.False(startup.ReadEnabled().Value);
        Assert.True(startup.SetEnabled(false).IsSuccess);
    }

    [Fact]
    public void The_dotnet_host_cannot_be_registered_as_the_app()
    {
        Directory.CreateDirectory(_directory);
        var host = Path.Combine(_directory, "dotnet.exe");
        File.WriteAllText(host, "");
        var startup = new WindowsStartupRegistration(host, _keyPath);

        Assert.Equal("Startup.InvalidExecutable", startup.SetEnabled(true).Error.Code);
        Assert.False(startup.ReadEnabled().Value);
    }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(_keyPath, throwOnMissingSubKey: false);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
