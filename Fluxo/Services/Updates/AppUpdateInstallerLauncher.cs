using System.Diagnostics;
using System.IO;

namespace Fluxo.Services.Updates;

public static class AppUpdateInstallerLauncher
{
    private const string InstallFolderArgument = "--install-folder";

    public static ProcessStartInfo CreateStartInfo(string installerPath, string installFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(installFolder);

        return new ProcessStartInfo
        {
            FileName = installerPath,
            Arguments = $"{InstallFolderArgument} {QuoteCommandLineArgument(Path.GetFullPath(installFolder))}",
            UseShellExecute = true
        };
    }

    private static string QuoteCommandLineArgument(string value) =>
        $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
}
