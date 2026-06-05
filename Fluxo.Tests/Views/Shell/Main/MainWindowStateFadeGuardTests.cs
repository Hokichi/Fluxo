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
        Assert.DoesNotContain("OutgoingMainPageHost", methodBody);
        Assert.DoesNotContain("AnalyticsDrawerLayer", methodBody);
        Assert.DoesNotContain("DrawerTabHost", methodBody);
    }

    [Fact]
    public void MainPageTransition_UsesSingleMainPageHostWithoutSnapshot()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private async Task TransitionToMainPageAsync(UIElement nextPage)");

        Assert.Contains("_isMainPageTransitionActive = true;", methodBody);
        Assert.Contains("_isMainPageTransitionActive = false;", methodBody);
        Assert.Contains("await FadeElementAsync(MainPageHost, 0", methodBody);
        Assert.Contains("MainPageHost.Content = nextPage;", methodBody);
        Assert.Contains("await FadeElementAsync(MainPageHost, 1", methodBody);
        Assert.DoesNotContain("CaptureElementSnapshot", source);
        Assert.DoesNotContain("OutgoingMainPageHost.Content", source);
    }

    [Fact]
    public void MainPageTransition_FadesOutBeforeAssigningIncomingPage()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private async Task TransitionToMainPageAsync(UIElement nextPage)");

        var fadeOutIndex = methodBody.IndexOf("await FadeElementAsync(MainPageHost, 0", StringComparison.Ordinal);
        var contentIndex = methodBody.IndexOf("MainPageHost.Content = nextPage;", StringComparison.Ordinal);
        var opacityResetIndex = methodBody.IndexOf("MainPageHost.Opacity = 0;", StringComparison.Ordinal);
        var fadeInIndex = methodBody.IndexOf("await FadeElementAsync(MainPageHost, 1", StringComparison.Ordinal);

        Assert.True(fadeOutIndex >= 0, "The current page should fade out first.");
        Assert.True(contentIndex > fadeOutIndex, "The next page should be assigned only after fade-out completes.");
        Assert.True(opacityResetIndex > contentIndex, "The incoming page should start transparent.");
        Assert.True(fadeInIndex > opacityResetIndex, "The incoming page should fade in after it is assigned at zero opacity.");
    }

    [Fact]
    public void MainPageTransition_UsesThreeHundredMillisecondDuration()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());

        Assert.Contains("private const int MainPageTransitionDuration = 300;", source);
        Assert.Contains("FadeElementAsync(MainPageHost, 0, EasingMode.EaseIn, MainPageTransitionDuration)", source);
        Assert.Contains("FadeElementAsync(MainPageHost, 1, EasingMode.EaseOut, MainPageTransitionDuration)", source);
    }

    [Fact]
    public void MainPageTransition_DoesNotBypassAnimationForReducedMotion()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private async Task TransitionToMainPageAsync(UIElement nextPage)");

        Assert.DoesNotContain("ShouldReduceMotion()", methodBody);
        Assert.DoesNotContain("ShowMainPageWithoutTransition(nextPage);", methodBody);
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
