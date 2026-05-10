using System.Diagnostics;
using System.IO;

namespace Fluxo.Installer.Models;

public static class InstallerElevationRelaunch
{
    public static bool ShouldRelaunch(
        bool isInteractive,
        bool isElevated,
        string? originalBundlePath,
        string? currentProcessPath)
    {
        if (!isInteractive || isElevated || string.IsNullOrWhiteSpace(originalBundlePath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentProcessPath))
        {
            return File.Exists(originalBundlePath);
        }

        return File.Exists(originalBundlePath)
            && !string.Equals(
                Path.GetFullPath(originalBundlePath),
                Path.GetFullPath(currentProcessPath),
                StringComparison.OrdinalIgnoreCase);
    }

    public static ProcessStartInfo CreateStartInfo(string originalBundlePath)
    {
        return new ProcessStartInfo(originalBundlePath)
        {
            UseShellExecute = true,
            Verb = "runas",
        };
    }
}
