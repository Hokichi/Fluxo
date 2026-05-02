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
        Assert.Contains("private EventHandler? _popupOverlayDeferredHideTickHandler;", source);
        Assert.DoesNotContain("private bool _isPopupOverlayHandoffPending;", source);
        Assert.DoesNotContain("private int _popupOverlayDeferredHideGeneration;", source);
    }

    [Fact]
    public void BeginPopupHandoff_DoesNotUseGlobalPendingFlag()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public void BeginPopupHandoff()");

        Assert.DoesNotContain("_isPopupOverlayHandoffPending", methodBody);
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
    public void HidePopupOverlay_UsesStateWithoutGlobalHandoffBranch()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public void HidePopupOverlay()");

        Assert.Contains("var hostAction = _popupOverlayHandoffState.OnPopupHidden();", methodBody);
        Assert.Contains("if (hostAction != PopupOverlayHostAction.HideOverlay)", methodBody);
        Assert.Contains("HidePopupOverlayCore();", methodBody);
        Assert.DoesNotContain("HidePopupOverlayForHandoff();", methodBody);
    }

    [Fact]
    public void HidePopupOverlayForHandoff_IsPublicAndSchedulesDeferredHideWhenRequested()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public void HidePopupOverlayForHandoff()");

        Assert.Contains("OnPopupHiddenForHandoff(out var deferredHideGeneration)", methodBody);
        Assert.Contains("if (hostAction != PopupOverlayHostAction.DeferHide)", methodBody);
        Assert.Contains("SchedulePopupOverlayDeferredHide(deferredHideGeneration);", methodBody);
    }

    [Fact]
    public void SchedulePopupOverlayDeferredHide_CapturesGenerationInTickHandler()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private void SchedulePopupOverlayDeferredHide(int deferredHideGeneration)");

        Assert.Contains("CancelPendingPopupOverlayDeferredHide();", methodBody);
        Assert.Contains("_popupOverlayDeferredHideTickHandler = (_, _) => OnPopupOverlayDeferredHideTimerTick(deferredHideGeneration);", methodBody);
        Assert.Contains("_popupOverlayDeferredHideTimer.Tick += _popupOverlayDeferredHideTickHandler;", methodBody);
        Assert.Contains("_popupOverlayDeferredHideTimer.Start();", methodBody);
    }

    [Fact]
    public void DeferredHideTimerTick_ResolvesGenerationAndOnlyHidesOnHideOverlay()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private void OnPopupOverlayDeferredHideTimerTick(int deferredHideGeneration)");

        Assert.Contains("CancelPendingPopupOverlayDeferredHide();", methodBody);
        Assert.Contains("ResolveDeferredHide(deferredHideGeneration)", methodBody);
        Assert.Contains("if (hostAction != PopupOverlayHostAction.HideOverlay)", methodBody);
        Assert.Contains("HidePopupOverlayCore();", methodBody);
    }

    [Fact]
    public void CancelPendingPopupOverlayDeferredHide_UnhooksCapturedTickHandler()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private void CancelPendingPopupOverlayDeferredHide()");

        Assert.Contains("_popupOverlayDeferredHideTimer.Stop();", methodBody);
        Assert.Contains("if (_popupOverlayDeferredHideTickHandler is null)", methodBody);
        Assert.Contains("_popupOverlayDeferredHideTimer.Tick -= _popupOverlayDeferredHideTickHandler;", methodBody);
        Assert.Contains("_popupOverlayDeferredHideTickHandler = null;", methodBody);
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
