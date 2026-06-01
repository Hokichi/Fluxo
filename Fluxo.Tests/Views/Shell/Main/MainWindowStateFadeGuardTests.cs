using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowStateFadeGuardTests
{
    [Fact]
    public void StateChangeFadeElements_ExcludeTabHost_WhenTabVisibilityTransitionIsActive()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private UIElement[] GetStateChangeFadeElements()");

        Assert.Contains("_isAnalyticsDrawerTabVisibilityTransitionActive", methodBody);
        Assert.Contains("? new UIElement[] { ContentGrid, AnalyticsDrawerLayer }", methodBody);
        Assert.Contains(": new UIElement[] { ContentGrid, AnalyticsDrawerLayer, DrawerTabHost };", methodBody);
    }

    [Fact]
    public void SetAnalyticsDrawerTabVisibility_ManagesTransitionFlagLifecycle()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private void SetAnalyticsDrawerTabVisibility(");

        Assert.Contains("_isAnalyticsDrawerTabVisibilityTransitionActive = false;", methodBody);
        Assert.Contains("_isAnalyticsDrawerTabVisibilityTransitionActive = true;", methodBody);
        Assert.Contains("tabAnimation.Completed += (_, _) =>", methodBody);
        Assert.Contains("if (visibilityToken != _analyticsDrawerTabVisibilityToken)", methodBody);

        var clearTransitionFlagCount =
            CountOccurrences(methodBody, "_isAnalyticsDrawerTabVisibilityTransitionActive = false;");
        Assert.True(
            clearTransitionFlagCount >= 2,
            "Expected transition flag to be cleared before animation setup and after animation completion.");
    }

    private static string ExtractMethodBodyBySignature(string source, string signatureMarker)
    {
        var signatureIndex = source.IndexOf(signatureMarker, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature '{signatureMarker}' was not found in MainWindow.xaml.cs.");

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

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ResolveMainWindowCodeBehindPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
            {
                var mainWindowCodeBehindPath = Path.Combine(
                    currentDirectory.FullName,
                    "Fluxo",
                    "Views",
                    "Shell",
                    "Main",
                    "MainWindow.xaml.cs");

                if (!File.Exists(mainWindowCodeBehindPath))
                {
                    throw new FileNotFoundException(
                        $"MainWindow.xaml.cs was not found at '{mainWindowCodeBehindPath}'.",
                        mainWindowCodeBehindPath);
                }

                return mainWindowCodeBehindPath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }
}
