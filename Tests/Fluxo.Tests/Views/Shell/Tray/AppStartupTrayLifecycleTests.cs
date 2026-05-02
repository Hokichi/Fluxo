using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Tray;

public sealed class AppStartupTrayLifecycleTests
{
    [Fact]
    public void OnStartup_InitializesTrayIconBeforeLaunchModeBranch()
    {
        var source = File.ReadAllText(ResolveAppCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "protected override async void OnStartup(StartupEventArgs e)");

        var initIndex = methodBody.IndexOf("EnsureTrayIconInitialized();", StringComparison.Ordinal);
        var branchIndex = methodBody.IndexOf("if (_launchInTrayMode)", StringComparison.Ordinal);

        Assert.True(initIndex >= 0, "OnStartup must initialize the tray icon when main window lifecycle begins.");
        Assert.True(branchIndex >= 0, "OnStartup launch-mode branch was not found.");
        Assert.True(initIndex < branchIndex,
            "EnsureTrayIconInitialized() must execute before launch-mode branching.");
    }

    private static string ExtractMethodBodyBySignature(string source, string signatureMarker)
    {
        var signatureIndex = source.IndexOf(signatureMarker, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature '{signatureMarker}' was not found in App.xaml.cs.");

        var openingBraceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(openingBraceIndex >= 0, $"Opening brace for method signature '{signatureMarker}' was not found.");

        var depth = 0;
        for (var index = openingBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
                continue;
            }

            if (source[index] != '}')
                continue;

            depth--;
            if (depth != 0)
                continue;

            return source.Substring(openingBraceIndex + 1, index - openingBraceIndex - 1);
        }

        throw new InvalidOperationException($"Closing brace for method signature '{signatureMarker}' was not found.");
    }

    private static string ResolveAppCodeBehindPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
            {
                var appCodeBehindPath = Path.Combine(currentDirectory.FullName, "Fluxo", "App.xaml.cs");

                if (!File.Exists(appCodeBehindPath))
                {
                    throw new FileNotFoundException(
                        $"App.xaml.cs was not found at '{appCodeBehindPath}'.",
                        appCodeBehindPath);
                }

                return appCodeBehindPath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }
}
