using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowStateFadeGuardTests
{
    [Fact]
    public void StateChangeFadeElements_IncludeContentHostAndFloatingNavigation()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private UIElement[] GetStateChangeFadeElements()");

        Assert.Contains("ContentGrid", methodBody);
        Assert.Contains("MainPageHost", methodBody);
        Assert.Contains("FloatingSideNavigationRail", methodBody);
        Assert.DoesNotContain("AnalyticsDrawerLayer", methodBody);
        Assert.DoesNotContain("DrawerTabHost", methodBody);
    }

    [Fact]
    public void HostedPageCrossfade_UsesDashboardPageHostWhenLeavingDashboard()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private async Task CrossfadeToHostedPageAsync(UIElement nextPage)");

        Assert.Contains("_isMainPageTransitionActive = true;", methodBody);
        Assert.Contains("_isMainPageTransitionActive = false;", methodBody);
        Assert.Contains("? DashboardPageHost", methodBody);
        Assert.Contains("DashboardPageHost.Visibility = Visibility.Collapsed;", methodBody);
        Assert.Contains("MainPageHost.Content = nextPage;", methodBody);
        Assert.Contains("await FadeElementAsync(MainPageHost, 1", methodBody);
        Assert.DoesNotContain("MainShellPage.Home", methodBody);
    }

    [Fact]
    public void DashboardShellCrossfade_RestoresDashboardContentWithoutHostedPage()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private async Task CrossfadeToDashboardShellAsync()");

        Assert.Contains("MainPageHost.Content = null;", methodBody);
        Assert.Contains("MainPageHost.Visibility = Visibility.Collapsed;", methodBody);
        Assert.Contains("DashboardPageHost.Visibility = Visibility.Visible;", methodBody);
        Assert.Contains("await FadeElementAsync(DashboardPageHost, 1", methodBody);
        Assert.DoesNotContain("PrepareHostedPageAsync", methodBody);
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
