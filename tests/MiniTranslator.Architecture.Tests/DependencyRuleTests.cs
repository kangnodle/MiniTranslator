using System.Reflection;
using MiniTranslator.Application.Settings;
using MiniTranslator.Domain.Common;
using MiniTranslator.Presentation.ViewModels;
using ClaudeCli = MiniTranslator.Adapters.ClaudeCli.ServiceCollectionExtensions;
using CodexCli = MiniTranslator.Adapters.CodexCli.ServiceCollectionExtensions;
using GoogleTranslate = MiniTranslator.Adapters.GoogleTranslate.ServiceCollectionExtensions;
using JsonSettings = MiniTranslator.Adapters.JsonSettings.ServiceCollectionExtensions;
using WindowsDesktop = MiniTranslator.Adapters.Windows.ServiceCollectionExtensions;

namespace MiniTranslator.Architecture.Tests;

/// <summary>
/// Enforces the Clean Architecture dependency rule at the assembly level:
/// dependencies point inward, the core stays free of frameworks and I/O, and adapters never know about each other.
/// </summary>
public class DependencyRuleTests
{
    private const string Domain = "MiniTranslator.Domain";
    private const string Application = "MiniTranslator.Application";

    private static readonly string[] Wpf = ["PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml"];
    private static readonly string[] IoLibraries =
        ["System.Net", "System.IO", "System.Text.Json", "Microsoft.Win32.Registry", "System.Diagnostics.Process", "System.Console"];
    private static readonly string[] Frameworks = ["Microsoft.Extensions", "CommunityToolkit"];

    public static TheoryData<string, string[]> AllowedWorkspaceReferences => new()
    {
        { Domain, [] },
        { Application, [Domain] },
        { "MiniTranslator.Presentation", [Domain, Application] },
        { "MiniTranslator.Adapters.ClaudeCli", [Domain, Application] },
        { "MiniTranslator.Adapters.CodexCli", [Domain, Application] },
        { "MiniTranslator.Adapters.GoogleTranslate", [Domain, Application] },
        { "MiniTranslator.Adapters.JsonSettings", [Domain, Application] },
        { "MiniTranslator.Adapters.Windows", [Domain, Application] },
    };

    [Theory]
    [MemberData(nameof(AllowedWorkspaceReferences))]
    public void Packages_only_depend_inward(string package, string[] allowed)
    {
        var workspaceReferences = ReferencesOf(package).Where(r => r.StartsWith("MiniTranslator.", StringComparison.Ordinal));

        Assert.All(workspaceReferences, r => Assert.Contains(r, allowed));
    }

    [Theory]
    [InlineData(Domain)]
    [InlineData(Application)]
    public void Core_is_free_of_frameworks_ui_and_io(string package)
    {
        AssertNoReferenceStartingWith(package, [.. Frameworks, .. Wpf, .. IoLibraries]);
    }

    [Fact]
    public void Presentation_is_ui_framework_agnostic()
    {
        AssertNoReferenceStartingWith("MiniTranslator.Presentation", [.. Wpf, .. IoLibraries, "Microsoft.Extensions"]);
    }

    [Fact]
    public void Settings_screen_depends_only_on_the_application_editor()
    {
        var constructor = Assert.Single(typeof(SettingsViewModel).GetConstructors());
        var dependency = Assert.Single(constructor.GetParameters());
        Assert.Equal(typeof(SettingsEditor), dependency.ParameterType);
    }

    [Theory]
    [InlineData("MiniTranslator.Adapters.ClaudeCli")]
    [InlineData("MiniTranslator.Adapters.CodexCli")]
    [InlineData("MiniTranslator.Adapters.GoogleTranslate")]
    [InlineData("MiniTranslator.Adapters.JsonSettings")]
    public void Portable_adapters_do_not_need_windows(string package)
    {
        AssertNoReferenceStartingWith(package, Wpf);
    }

    private static void AssertNoReferenceStartingWith(string package, string[] forbidden)
    {
        var offending = ReferencesOf(package)
            .Where(r => forbidden.Any(f => r.StartsWith(f, StringComparison.Ordinal)))
            .ToList();

        Assert.True(offending.Count == 0, $"{package} must not reference: {string.Join(", ", offending)}");
    }

    private static IEnumerable<string> ReferencesOf(string package) =>
        Packages[package].GetReferencedAssemblies().Select(a => a.Name!);

    private static readonly IReadOnlyDictionary<string, Assembly> Packages = new[]
    {
        typeof(Error).Assembly,
        typeof(SettingsStore).Assembly,
        typeof(TranslatorViewModel).Assembly,
        typeof(ClaudeCli).Assembly,
        typeof(CodexCli).Assembly,
        typeof(GoogleTranslate).Assembly,
        typeof(JsonSettings).Assembly,
        typeof(WindowsDesktop).Assembly,
    }.ToDictionary(a => a.GetName().Name!);
}
