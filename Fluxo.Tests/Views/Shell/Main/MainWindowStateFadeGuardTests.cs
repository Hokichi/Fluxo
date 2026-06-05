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
        Assert.Contains("OutgoingMainPageHost.Visibility = Visibility.Collapsed;", methodBody);
        Assert.Contains("OutgoingMainPageHost.Content = CaptureElementSnapshot(outgoingElement);", methodBody);
        Assert.Contains("MainPageHost.Content = nextPage;", methodBody);
        Assert.Contains("var fadeInTask = FadeElementAsync(MainPageHost, 1", methodBody);
        Assert.DoesNotContain("OutgoingMainPageHost.Content = MainPageHost.Content;", methodBody);
        Assert.DoesNotContain("MainShellPage.Home", methodBody);
    }

    [Fact]
    public void HostedPageCrossfade_StartsFadeOutAndFadeInTogetherAfterIncomingPageIsReady()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private async Task CrossfadeToHostedPageAsync(UIElement nextPage)");

        var contentIndex = methodBody.LastIndexOf("MainPageHost.Content = nextPage;", StringComparison.Ordinal);
        var showIndex = methodBody.LastIndexOf("MainPageHost.Visibility = Visibility.Visible;", StringComparison.Ordinal);
        var renderReadyIndex = methodBody.IndexOf("await AwaitElementRenderAsync(MainPageHost);", StringComparison.Ordinal);
        var fadeOutTaskIndex = methodBody.IndexOf("var fadeOutTask = FadeElementAsync(OutgoingMainPageHost, 0", StringComparison.Ordinal);
        var fadeInTaskIndex = methodBody.IndexOf("var fadeInTask = FadeElementAsync(MainPageHost, 1", StringComparison.Ordinal);
        var awaitBothIndex = methodBody.IndexOf("await Task.WhenAll(fadeOutTask, fadeInTask);", StringComparison.Ordinal);
        var collapseIndex = methodBody.IndexOf("OutgoingMainPageHost.Visibility = Visibility.Collapsed;", StringComparison.Ordinal);

        Assert.True(contentIndex >= 0, "The incoming hosted page should be assigned before either fade begins.");
        Assert.True(showIndex > contentIndex, "The incoming hosted host should become visible before either fade begins.");
        Assert.True(renderReadyIndex > showIndex, "The incoming hosted host should be rendered at zero opacity before either fade begins.");
        Assert.True(fadeOutTaskIndex > renderReadyIndex, "The outgoing page fade should wait until the incoming host has rendered.");
        Assert.True(fadeInTaskIndex > fadeOutTaskIndex, "The incoming page fade should be started without awaiting the outgoing fade first.");
        Assert.True(awaitBothIndex > fadeInTaskIndex, "The transition should await both fade tasks together.");
        Assert.True(collapseIndex > awaitBothIndex, "The outgoing host should stay visible until the crossfade completes.");
        Assert.DoesNotContain("await FadeElementAsync(currentPageElement, 0", methodBody);
    }

    [Fact]
    public void DashboardShellCrossfade_RestoresDashboardContentWithoutHostedPage()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private async Task CrossfadeToDashboardShellAsync()");

        Assert.Contains("OutgoingMainPageHost.Content = CaptureElementSnapshot(MainPageHost);", methodBody);
        Assert.Contains("MainPageHost.Content = null;", methodBody);
        Assert.Contains("MainPageHost.Visibility = Visibility.Collapsed;", methodBody);
        Assert.Contains("DashboardPageHost.Visibility = Visibility.Visible;", methodBody);
        Assert.Contains("var fadeInTask = FadeElementAsync(DashboardPageHost, 1", methodBody);
        Assert.DoesNotContain("PrepareHostedPageAsync", methodBody);
    }

    [Fact]
    public void DashboardShellCrossfade_StartsFadeOutAndFadeInTogether()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private async Task CrossfadeToDashboardShellAsync()");

        var showIndex = methodBody.IndexOf("DashboardPageHost.Visibility = Visibility.Visible;", StringComparison.Ordinal);
        var renderReadyIndex = methodBody.IndexOf("await AwaitElementRenderAsync(DashboardPageHost);", StringComparison.Ordinal);
        var fadeOutTaskIndex = methodBody.IndexOf("var fadeOutTask = FadeElementAsync(OutgoingMainPageHost, 0", StringComparison.Ordinal);
        var fadeInTaskIndex = methodBody.IndexOf("var fadeInTask = FadeElementAsync(DashboardPageHost, 1", StringComparison.Ordinal);
        var awaitBothIndex = methodBody.IndexOf("await Task.WhenAll(fadeOutTask, fadeInTask);", StringComparison.Ordinal);
        var snapshotIndex = methodBody.IndexOf("OutgoingMainPageHost.Content = CaptureElementSnapshot(MainPageHost);", StringComparison.Ordinal);
        var clearLiveContentIndex = methodBody.IndexOf("MainPageHost.Content = null;", StringComparison.Ordinal);
        var clearSnapshotIndex = methodBody.LastIndexOf("OutgoingMainPageHost.Content = null;", StringComparison.Ordinal);
        var collapseIndex = methodBody.LastIndexOf("OutgoingMainPageHost.Visibility = Visibility.Collapsed;", StringComparison.Ordinal);

        Assert.True(snapshotIndex >= 0, "The outgoing hosted page should be captured before live content is cleared.");
        Assert.True(clearLiveContentIndex > snapshotIndex, "The live hosted content should clear only after its outgoing snapshot is ready.");
        Assert.True(showIndex > clearLiveContentIndex, "The dashboard host should become visible after the outgoing snapshot is ready.");
        Assert.True(renderReadyIndex > showIndex, "The dashboard host should be rendered at zero opacity before either fade begins.");
        Assert.True(fadeOutTaskIndex > renderReadyIndex, "The hosted page fade should wait until the dashboard host has rendered.");
        Assert.True(fadeInTaskIndex > fadeOutTaskIndex, "The dashboard fade should be started without awaiting the hosted page fade first.");
        Assert.True(awaitBothIndex > fadeInTaskIndex, "The transition should await both fade tasks together.");
        Assert.True(clearSnapshotIndex > awaitBothIndex, "The outgoing snapshot should stay present until the crossfade completes.");
        Assert.True(collapseIndex > clearSnapshotIndex, "The outgoing snapshot host should collapse only after its content is cleared.");
        Assert.DoesNotContain("await FadeElementAsync(MainPageHost, 0", methodBody);
    }

    [Fact]
    public void MainPageCrossfade_UsesThreeHundredMillisecondDuration()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());

        Assert.Contains("private const int MainPageTransitionDuration = 300;", source);
        Assert.Contains("FadeElementAsync(OutgoingMainPageHost, 0, EasingMode.EaseIn, MainPageTransitionDuration)", source);
        Assert.Contains("FadeElementAsync(MainPageHost, 1, EasingMode.EaseOut, MainPageTransitionDuration)", source);
        Assert.Contains("FadeElementAsync(DashboardPageHost, 1, EasingMode.EaseOut, MainPageTransitionDuration)", source);
    }

    [Fact]
    public void MainPageCrossfade_DoesNotBypassAnimationForReducedMotion()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var hostedBody = ExtractMethodBodyBySignature(source, "private async Task CrossfadeToHostedPageAsync(UIElement nextPage)");
        var dashboardBody = ExtractMethodBodyBySignature(source, "private async Task CrossfadeToDashboardShellAsync()");

        Assert.DoesNotContain("ShouldReduceMotion()", hostedBody);
        Assert.DoesNotContain("ShouldReduceMotion()", dashboardBody);
        Assert.DoesNotContain("ShowHostedPageWithoutTransition(nextPage);", hostedBody);
        Assert.DoesNotContain("ShowDashboardShellWithoutTransition();", dashboardBody);
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
