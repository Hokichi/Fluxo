using Fluxo.Views.CustomControls;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class PopupOverlayHandoffStateTests
{
    [Fact]
    public void OnPopupShown_FirstShow_ReturnsShowOverlayAndCountIsOne()
    {
        var state = new PopupOverlayHandoffState();

        var action = state.OnPopupShown();

        Assert.Equal(PopupOverlayHostAction.ShowOverlay, action);
        Assert.Equal(1, state.ActivePopupCount);
    }

    [Fact]
    public void OnPopupHidden_LastHideWithoutHandoff_ReturnsHideOverlayAndCountIsZero()
    {
        var state = new PopupOverlayHandoffState();
        state.OnPopupShown();

        var action = state.OnPopupHidden();

        Assert.Equal(PopupOverlayHostAction.HideOverlay, action);
        Assert.Equal(0, state.ActivePopupCount);
    }

    [Fact]
    public void HandoffCloseThenOpen_DefersHide_ConsumesHandoff_AndDoesNotRestartShow()
    {
        var state = new PopupOverlayHandoffState();
        state.OnPopupShown();

        var closeAction = state.OnPopupHidden(allowHandoff: true);
        var openAction = state.OnPopupShown();
        var deferredResolutionAction = state.ResolveDeferredHide();

        Assert.Equal(PopupOverlayHostAction.DeferHide, closeAction);
        Assert.Equal(PopupOverlayHostAction.None, openAction);
        Assert.Equal(PopupOverlayHostAction.None, deferredResolutionAction);
        Assert.Equal(1, state.ActivePopupCount);
    }

    [Fact]
    public void ResolveDeferredHide_WhenNoReopen_ReturnsHideOverlayAndClearsStaleHandoff()
    {
        var state = new PopupOverlayHandoffState();
        state.OnPopupShown();
        state.OnPopupHidden(allowHandoff: true);

        var resolveAction = state.ResolveDeferredHide();
        var nextShowAction = state.OnPopupShown();

        Assert.Equal(PopupOverlayHostAction.HideOverlay, resolveAction);
        Assert.Equal(PopupOverlayHostAction.ShowOverlay, nextShowAction);
        Assert.Equal(1, state.ActivePopupCount);
    }

    [Fact]
    public void OnPopupHidden_ExtraHideCalls_DoNotGoNegative()
    {
        var state = new PopupOverlayHandoffState();

        var extraHideBeforeShow = state.OnPopupHidden();
        state.OnPopupShown();
        state.OnPopupHidden();
        var extraHideAfterLastHide = state.OnPopupHidden();

        Assert.Equal(PopupOverlayHostAction.None, extraHideBeforeShow);
        Assert.Equal(PopupOverlayHostAction.None, extraHideAfterLastHide);
        Assert.Equal(0, state.ActivePopupCount);
    }
}
