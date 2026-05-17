using System.IO;

namespace Fluxo.Installer.Models;

public static class InstallerOperationModeDetector
{
    private const string RepairerExecutableName = "fluxo.Repairer.exe";
    private const string ProductExecutablePrefix = "fluxo";
    private const string RepairToken = "repair";

    public static InstallerOperationMode Detect(
        string? originalSourcePath,
        string? sourceProcessPath,
        string? processPath)
    {
        return IsMaintenanceExecutable(originalSourcePath)
               || IsMaintenanceExecutable(sourceProcessPath)
               || IsMaintenanceExecutable(processPath)
            ? InstallerOperationMode.Maintenance
            : InstallerOperationMode.Install;
    }

    public static InstallerOperationMode Detect(string? originalSourcePath, string? processPath)
    {
        return Detect(originalSourcePath, null, processPath);
    }

    /// <summary>
    /// WiX can set <c>WixBundleOriginalSource</c> to the registered bundle (installer) while
    /// <c>WixBundleSourceProcessPath</c> is the executable that actually launched Burn (e.g. fluxo.Repairer.exe).
    /// The view model needs the repairer path for maintenance UI, install-folder resolution, and repairer copy source.
    /// </summary>
    public static string SelectBundleExecutablePathForViewModel(
        string? wixBundleSourceProcessPath,
        string? wixBundleOriginalSource,
        string fallbackBundlePath)
    {
        foreach (var candidate in new[] { wixBundleSourceProcessPath, wixBundleOriginalSource })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (Detect(candidate, null, null) == InstallerOperationMode.Maintenance)
            {
                return candidate;
            }
        }

        return fallbackBundlePath;
    }

    private static bool IsMaintenanceExecutable(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var executableName = Path.GetFileName(executablePath);
        if (string.Equals(executableName, RepairerExecutableName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(Path.GetExtension(executableName), ".exe", StringComparison.OrdinalIgnoreCase)
            && executableName.StartsWith(ProductExecutablePrefix, StringComparison.OrdinalIgnoreCase)
            && executableName.Contains(RepairToken, StringComparison.OrdinalIgnoreCase);
    }
}
