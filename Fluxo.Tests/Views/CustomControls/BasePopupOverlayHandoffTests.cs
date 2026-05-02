using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class BasePopupOverlayHandoffTests
{
    [Fact]
    public void BasePopup_DefinesHandoffStateAndDeferredHideSchedulerFields()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());

        Assert.Contains("private readonly PopupOverlayHandoffState _popupOverlayHandoffState = new();", source);
        Assert.Contains("private readonly DispatcherTimer _popupOverlayDeferredHideTimer = new()", source);
        Assert.Contains("private EventHandler? _popupOverlayDeferredHideTickHandler;", source);
    }

    [Fact]
    public void CloseForPopupHandoff_MarksCloseAsHandoffBeforeClosing()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "protected void CloseForPopupHandoff()");

        Assert.Contains("_isClosingForPopupHandoff = true;", methodBody);
        Assert.Contains("Close();", methodBody);
    }

    [Fact]
    public void OnClosing_RoutesOwnerOverlayHideThroughHelper()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "protected override void OnClosing(CancelEventArgs e)");

        Assert.Contains("RouteOwnerPopupOverlayHide();", methodBody);
    }

    [Fact]
    public void RouteOwnerPopupOverlayHide_UsesExplicitNormalAndHandoffHostCalls()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private void RouteOwnerPopupOverlayHide()");

        Assert.Contains("_isClosingForPopupHandoff", methodBody);
        Assert.Contains("_popupHost.HidePopupOverlayForHandoff();", methodBody);
        Assert.Contains("_popupHost.HidePopupOverlay();", methodBody);
    }

    [Fact]
    public void BeginPopupHandoff_MarksPendingHandoffForChildPopupStacking()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public void BeginPopupHandoff()");

        Assert.Contains("_isPopupOverlayHandoffPending = true;", methodBody);
    }

    [Fact]
    public void HidePopupOverlay_ConsumesPendingHandoffMarkerBeforeNormalHide()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public void HidePopupOverlay()");

        Assert.Contains("if (_isPopupOverlayHandoffPending)", methodBody);
        Assert.Contains("HidePopupOverlayForHandoff();", methodBody);
        Assert.Contains("var hostAction = _popupOverlayHandoffState.OnPopupHidden();", methodBody);
    }

    [Fact]
    public void HidePopupOverlayForHandoff_UsesStateAndDeferredScheduling()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public void HidePopupOverlayForHandoff()");

        Assert.Contains("OnPopupHiddenForHandoff(out var deferredHideGeneration)", methodBody);
        Assert.Contains("if (hostAction != PopupOverlayHostAction.DeferHide)", methodBody);
        Assert.Contains("SchedulePopupOverlayDeferredHide(deferredHideGeneration);", methodBody);
    }

    [Fact]
    public void SchedulePopupOverlayDeferredHide_CapturesGenerationInTickHandler()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private void SchedulePopupOverlayDeferredHide(int deferredHideGeneration)");

        Assert.Contains("CancelPendingPopupOverlayDeferredHide();", methodBody);
        Assert.Contains("_popupOverlayDeferredHideTickHandler = (_, _) => OnPopupOverlayDeferredHideTimerTick(deferredHideGeneration);", methodBody);
        Assert.Contains("_popupOverlayDeferredHideTimer.Start();", methodBody);
    }

    [Fact]
    public void DeferredHideTimerTick_ResolvesCapturedGeneration()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "private void OnPopupOverlayDeferredHideTimerTick(int deferredHideGeneration)");

        Assert.Contains("ResolveDeferredHide(deferredHideGeneration)", methodBody);
        Assert.Contains("HidePopupOverlayCore();", methodBody);
    }

    private static string ExtractMethodBodyBySignature(string source, string signatureMarker)
    {
        var signatureIndex = source.IndexOf(signatureMarker, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature '{signatureMarker}' was not found in BasePopup.cs.");

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

    private static string ResolveBasePopupPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
            {
                var basePopupPath = Path.Combine(
                    currentDirectory.FullName,
                    "Fluxo",
                    "Views",
                    "CustomControls",
                    "BasePopup.cs");

                if (!File.Exists(basePopupPath))
                {
                    throw new FileNotFoundException(
                        $"BasePopup.cs was not found at '{basePopupPath}'.",
                        basePopupPath);
                }

                return basePopupPath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }
}
