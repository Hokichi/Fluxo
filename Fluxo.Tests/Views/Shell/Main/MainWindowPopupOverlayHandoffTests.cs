using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowPopupOverlayHandoffTests
{
    [Fact]
    public void MainWindowCodeBehind_DefinesPopupOverlayHandoffStateAndDeferredHideFields()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());

        Assert.Contains("private readonly PopupOverlayHandoffState _popupOverlayHandoffState = new();", source);
        Assert.Contains("private readonly DispatcherTimer _popupOverlayDeferredHideTimer = new()", source);
        Assert.Contains("private int _popupOverlayDeferredHideGeneration;", source);
    }

    [Fact]
    public void BeginPopupHandoff_MarksHandoffOnHost()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public void BeginPopupHandoff()");

        Assert.Contains("_isPopupOverlayHandoffPending = true;", methodBody);
    }

    [Fact]
    public void ShowPopupOverlay_UsesStateAndOnlyAnimatesOnShowOverlay()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public void ShowPopupOverlay()");

        Assert.Contains("CancelPendingPopupOverlayDeferredHide();", methodBody);
        Assert.Contains("var hostAction = _popupOverlayHandoffState.OnPopupShown();", methodBody);
        Assert.Contains("if (hostAction != PopupOverlayHostAction.ShowOverlay)", methodBody);
        Assert.Contains("PopupOverlay.BeginAnimation(OpacityProperty, null);", methodBody);
    }

    [Fact]
    public void HidePopupOverlay_UsesStateAndHandoffHelper()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public void HidePopupOverlay()");

        Assert.Contains("if (_isPopupOverlayHandoffPending)", methodBody);
        Assert.Contains("HidePopupOverlayForHandoff();", methodBody);
        Assert.Contains("var hostAction = _popupOverlayHandoffState.OnPopupHidden();", methodBody);
        Assert.Contains("if (hostAction != PopupOverlayHostAction.HideOverlay)", methodBody);
        Assert.Contains("HidePopupOverlayCore();", methodBody);
    }

    [Fact]
    public void HidePopupOverlayForHandoff_SchedulesDeferredHideWhenRequested()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private void HidePopupOverlayForHandoff()");

        Assert.Contains("OnPopupHiddenForHandoff(out var deferredHideGeneration)", methodBody);
        Assert.Contains("if (hostAction != PopupOverlayHostAction.DeferHide)", methodBody);
        Assert.Contains("_popupOverlayDeferredHideGeneration = deferredHideGeneration;", methodBody);
        Assert.Contains("_popupOverlayDeferredHideTimer.Start();", methodBody);
    }

    [Fact]
    public void DeferredHideTimerTick_ResolvesGenerationAndOnlyHidesOnHideOverlay()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private void OnPopupOverlayDeferredHideTimerTick(");

        Assert.Contains("_popupOverlayDeferredHideTimer.Stop();", methodBody);
        Assert.Contains("ResolveDeferredHide(_popupOverlayDeferredHideGeneration)", methodBody);
        Assert.Contains("if (hostAction != PopupOverlayHostAction.HideOverlay)", methodBody);
        Assert.Contains("HidePopupOverlayCore();", methodBody);
    }

    [Fact]
    public void HidePopupOverlayCore_GuardsCollapseWhenOverlayBecomesActiveAgain()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private void HidePopupOverlayCore()");

        Assert.Contains("PopupOverlay.BeginAnimation(OpacityProperty, null);", methodBody);
        Assert.Contains("if (_popupOverlayHandoffState.ActivePopupCount > 0)", methodBody);
        Assert.Contains("PopupOverlay.Visibility = Visibility.Collapsed;", methodBody);
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
