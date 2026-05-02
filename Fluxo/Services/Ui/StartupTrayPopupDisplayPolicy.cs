namespace Fluxo.Services.Ui;

public sealed class StartupTrayPopupDisplayPolicy
{
    public bool ShouldShow(bool launchInTrayMode, bool alreadyShownThisProcess, bool hasSummary)
    {
        return launchInTrayMode && !alreadyShownThisProcess && hasSummary;
    }
}
