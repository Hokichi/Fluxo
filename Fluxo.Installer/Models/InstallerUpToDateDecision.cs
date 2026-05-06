namespace Fluxo.Installer.Models;

public static class InstallerUpToDateDecision
{
    public static bool ShouldSkipInstall(
        InstallerOperationMode operationMode,
        int detectStatus,
        string? currentBundleVersion,
        string? highestDetectedInstalledVersion,
        Func<string, string, int> compareVersions)
    {
        if (operationMode != InstallerOperationMode.Install || detectStatus != 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentBundleVersion)
            || string.IsNullOrWhiteSpace(highestDetectedInstalledVersion))
        {
            return false;
        }

        try
        {
            return compareVersions(highestDetectedInstalledVersion, currentBundleVersion) >= 0;
        }
        catch
        {
            return false;
        }
    }
}
