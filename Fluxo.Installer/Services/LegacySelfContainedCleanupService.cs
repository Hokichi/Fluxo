using System.IO;

namespace Fluxo.Installer.Services;

public sealed record LegacyCleanupResult(bool Success, string Message);

public interface ILegacySelfContainedCleanupService
{
    LegacyCleanupResult Cleanup(string installFolder);
}

public sealed class LegacySelfContainedCleanupService : ILegacySelfContainedCleanupService
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly HashSet<string> LegacyRuntimeFiles = new(PathComparer)
    {
        "clrjit.dll",
        "coreclr.dll",
        "createdump.exe",
        "hostfxr.dll",
        "hostpolicy.dll",
        "Microsoft.CSharp.dll",
        "Microsoft.VisualBasic.Core.dll",
        "mscordaccore.dll",
        "mscordbi.dll",
        "mscorlib.dll",
        "PresentationCore.dll",
        "PresentationFramework.dll",
        "PresentationNative_cor3.dll",
        "System.Private.CoreLib.dll",
        "System.Private.Uri.dll",
        "System.Private.Xml.dll",
        "System.Private.Xml.Linq.dll",
        "System.Xaml.dll",
        "WindowsBase.dll",
        "wpfgfx_cor3.dll",
    };

    private static readonly HashSet<string> RootFilesToKeep = new(PathComparer)
    {
        "fluxo.db",
        "fluxo.deps.json",
        "fluxo.dll",
        "fluxo.exe",
        "fluxo.runtimeconfig.json",
        "fluxo.Repairer.exe",
    };

    private readonly Func<string, bool> directoryExists;
    private readonly Func<string, bool> fileExists;
    private readonly Func<string, string, IEnumerable<string>> enumerateFiles;
    private readonly Action<string> deleteFile;

    public LegacySelfContainedCleanupService(
        Func<string, bool>? directoryExists = null,
        Func<string, bool>? fileExists = null,
        Func<string, string, IEnumerable<string>>? enumerateFiles = null,
        Action<string>? deleteFile = null)
    {
        this.directoryExists = directoryExists ?? Directory.Exists;
        this.fileExists = fileExists ?? File.Exists;
        this.enumerateFiles = enumerateFiles ?? ((path, pattern) => Directory.EnumerateFiles(path, pattern));
        this.deleteFile = deleteFile ?? File.Delete;
    }

    public LegacyCleanupResult Cleanup(string installFolder)
    {
        if (string.IsNullOrWhiteSpace(installFolder))
        {
            return new LegacyCleanupResult(false, "Installation folder is missing.");
        }

        try
        {
            if (!directoryExists(installFolder))
            {
                return new LegacyCleanupResult(true, "Installation folder does not exist.");
            }

            var deletedCount = 0;
            foreach (var file in enumerateFiles(installFolder, "*").ToArray())
            {
                if (!ShouldDeleteRootFile(installFolder, file))
                {
                    continue;
                }

                deleteFile(file);
                deletedCount++;
            }

            return new LegacyCleanupResult(true, $"Removed {deletedCount} legacy application file(s).");
        }
        catch (Exception ex)
        {
            return new LegacyCleanupResult(false, ex.Message);
        }
    }

    private bool ShouldDeleteRootFile(string installFolder, string file)
    {
        var fileName = Path.GetFileName(file);
        if (RootFilesToKeep.Contains(fileName))
        {
            return false;
        }

        if (string.Equals(Path.GetExtension(fileName), ".log", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (LegacyRuntimeFiles.Contains(fileName))
        {
            return true;
        }

        if (!string.Equals(Path.GetExtension(fileName), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fileExists(Path.Combine(installFolder, "libs", fileName))
            || fileExists(Path.Combine(installFolder, "vendor", fileName));
    }
}
