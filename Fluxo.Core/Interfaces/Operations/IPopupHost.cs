namespace Fluxo.Core.Interfaces.Operations;

public interface IPopupHost
{
    void ShowPopupOverlay();
    void HidePopupOverlay();
    void HidePopupOverlayForHandoff();
    void BeginPopupHandoff();
}
