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
