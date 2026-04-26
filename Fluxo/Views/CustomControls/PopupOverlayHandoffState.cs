namespace Fluxo.Views.CustomControls;

public enum PopupOverlayHostAction
{
    None,
    ShowOverlay,
    HideOverlay,
    DeferHide
}

public sealed class PopupOverlayHandoffState
{
    private int _activePopupCount;
    private bool _hasDeferredHide;

    public int ActivePopupCount => _activePopupCount;

    public PopupOverlayHostAction OnPopupShown()
    {
        if (_hasDeferredHide)
        {
            _hasDeferredHide = false;
            _activePopupCount++;
            return PopupOverlayHostAction.None;
        }

        _activePopupCount++;
        return _activePopupCount == 1
            ? PopupOverlayHostAction.ShowOverlay
            : PopupOverlayHostAction.None;
    }

    public PopupOverlayHostAction OnPopupHidden(bool allowHandoff = false)
    {
        if (_activePopupCount == 0)
            return PopupOverlayHostAction.None;

        _activePopupCount--;
        if (_activePopupCount > 0)
            return PopupOverlayHostAction.None;

        if (allowHandoff)
        {
            _hasDeferredHide = true;
            return PopupOverlayHostAction.DeferHide;
        }

        _hasDeferredHide = false;
        return PopupOverlayHostAction.HideOverlay;
    }

    public PopupOverlayHostAction ResolveDeferredHide()
    {
        if (!_hasDeferredHide)
            return PopupOverlayHostAction.None;

        _hasDeferredHide = false;
        return _activePopupCount == 0
            ? PopupOverlayHostAction.HideOverlay
            : PopupOverlayHostAction.None;
    }
}
