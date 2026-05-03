namespace Fluxo.Installer.Models;

public enum InstallerState
{
    Welcome,
    Installing,
    Verifying,
    FinishedSuccess,
    FinishedUpToDate,
    FinishedFailed,
    FinishedCancelled,
}
