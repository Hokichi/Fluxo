using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Fluxo.Infrastructure;

internal static class MachineWideDataDirectoryPreparer
{
    private static readonly SecurityIdentifier UsersSid = new(WellKnownSidType.BuiltinUsersSid, null);
    private static readonly FileSystemRights SharedDataRights = FileSystemRights.Modify;
    private static readonly string[] RuntimeFileNames =
    [
        "fluxo.db",
        "fluxo.db-journal",
        "fluxo.db-wal",
        "fluxo.db-shm",
    ];

    public static void Prepare(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        Directory.CreateDirectory(directoryPath);
        GrantDirectoryModifyAccess(directoryPath);

        foreach (var filePath in EnumerateExistingRuntimeFiles(directoryPath))
            GrantFileModifyAccess(filePath);
    }

    internal static IEnumerable<string> EnumerateExistingRuntimeFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return [];

        return RuntimeFileNames
            .Select(fileName => Path.Combine(directoryPath, fileName))
            .Where(File.Exists)
            .ToArray();
    }

    private static void GrantDirectoryModifyAccess(string directoryPath)
    {
        var directoryInfo = new DirectoryInfo(directoryPath);
        var security = directoryInfo.GetAccessControl();
        var rule = new FileSystemAccessRule(
            UsersSid,
            SharedDataRights,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow);

        security.AddAccessRule(rule);
        directoryInfo.SetAccessControl(security);
    }

    private static void GrantFileModifyAccess(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var security = fileInfo.GetAccessControl();
        var rule = new FileSystemAccessRule(
            UsersSid,
            SharedDataRights,
            InheritanceFlags.None,
            PropagationFlags.None,
            AccessControlType.Allow);

        security.AddAccessRule(rule);
        fileInfo.SetAccessControl(security);
    }
}
