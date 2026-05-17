using System.Diagnostics;
using System.IO;

namespace Fluxo.Installer.Models;

public static class InstallerElevationRelaunch
{
    /// <summary>
    /// When true, the interactive bootstrapper should relaunch with <c>runas</c> so file operations
    /// under Program Files (repairer copy, rollback cleanup) run elevated. Unlike
    /// <see cref="ShouldRelaunch"/>, this applies even when the bundle path matches the current process.
    /// </summary>
    public static bool ShouldRelaunchForElevation(bool isInteractive, bool isElevated, string? bundlePath) =>
        isInteractive
        && !isElevated
        && !string.IsNullOrWhiteSpace(bundlePath)
        && File.Exists(bundlePath);

    public static string? SelectBundlePathForElevationRelaunch(
        string? wixBundleSourceProcessPath,
        string? wixBundleOriginalSource,
        string? processPath)
    {
        var fallbackPath = FirstNonWhiteSpace(
            wixBundleOriginalSource,
            wixBundleSourceProcessPath,
            processPath);

        return fallbackPath is null
            ? null
            : InstallerOperationModeDetector.SelectBundleExecutablePathForViewModel(
                wixBundleSourceProcessPath,
                wixBundleOriginalSource,
                fallbackPath);
    }

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

    public static ProcessStartInfo CreateStartInfo(string originalBundlePath, string? arguments = null)
    {
        return new ProcessStartInfo(originalBundlePath)
        {
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true,
            Verb = "runas",
        };
    }

    private static string? FirstNonWhiteSpace(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
