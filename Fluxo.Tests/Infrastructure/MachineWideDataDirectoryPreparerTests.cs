using System.Security.AccessControl;
using System.Security.Principal;
using Fluxo.Infrastructure;
using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class MachineWideDataDirectoryPreparerTests
{
    [WindowsOnlyFact]
    public void Prepare_GrantsUsersModifyAccessToDirectoryAndExistingRuntimeFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fluxo-acl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var dbPath = Path.Combine(directory, "fluxo.db");
        var journalPath = Path.Combine(directory, "fluxo.db-journal");
        var walPath = Path.Combine(directory, "fluxo.db-wal");
        File.WriteAllText(dbPath, string.Empty);
        File.WriteAllText(journalPath, string.Empty);
        File.WriteAllText(walPath, string.Empty);

        try
        {
            MachineWideDataDirectoryPreparer.Prepare(directory);

            var directorySecurity = new DirectoryInfo(directory).GetAccessControl();
            Assert.Contains(
                GetExplicitUsersRules(directorySecurity),
                rule => HasModify(rule)
                        && !rule.IsInherited
                        && rule.InheritanceFlags.HasFlag(InheritanceFlags.ContainerInherit)
                        && rule.InheritanceFlags.HasFlag(InheritanceFlags.ObjectInherit));

            foreach (var path in new[] { dbPath, journalPath, walPath })
            {
                var fileSecurity = new FileInfo(path).GetAccessControl();
                Assert.Contains(
                    GetExplicitUsersRules(fileSecurity),
                    rule => HasModify(rule) && !rule.IsInherited);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void EnumerateExistingRuntimeFiles_ReturnsOnlyFluxoRuntimeStateFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"fluxo-runtime-files-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var expected = new[]
        {
            Path.Combine(directory, "fluxo.db"),
            Path.Combine(directory, "fluxo.db-journal"),
            Path.Combine(directory, "fluxo.db-wal"),
            Path.Combine(directory, "fluxo.db-shm"),
        };
        foreach (var path in expected)
            File.WriteAllText(path, string.Empty);

        var ignored = Path.Combine(directory, "unrelated.txt");
        File.WriteAllText(ignored, string.Empty);

        try
        {
            var files = MachineWideDataDirectoryPreparer
                .EnumerateExistingRuntimeFiles(directory)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.Equal(
                expected.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase),
                files);
            Assert.DoesNotContain(ignored, files);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static IEnumerable<FileSystemAccessRule> GetExplicitUsersRules(FileSystemSecurity security)
    {
        var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        return security
            .GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .OfType<FileSystemAccessRule>()
            .Where(rule => usersSid.Equals(rule.IdentityReference)
                           && rule.AccessControlType == AccessControlType.Allow);
    }

    private static bool HasModify(FileSystemAccessRule rule) =>
        (rule.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify;

    private sealed class WindowsOnlyFactAttribute : FactAttribute
    {
        public WindowsOnlyFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
                Skip = "Windows-only ACL test.";
        }
    }
}
