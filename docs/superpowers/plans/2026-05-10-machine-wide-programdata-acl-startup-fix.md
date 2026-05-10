# Machine-Wide ProgramData ACL Startup Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep Fluxo's database machine-wide while allowing standard users to launch installed builds and run startup migrations without crashing.

**Architecture:** Add a focused ProgramData ACL preparer in the app, call it before backup/migration, and make the MSI apply the same policy from an elevated install/repair context. Separately harden `FluxoMessageBox` owner resolution so startup error reporting cannot crash while reporting the original failure.

**Tech Stack:** .NET 10 WPF, EF Core SQLite, xUnit, WiX Toolset v7 MSI authoring, Windows ACL APIs.

---

## File Structure

- Create `Fluxo/Infrastructure/MachineWideDataDirectoryPreparer.cs`
  - Owns Windows ACL preparation for machine-wide runtime data directories.
  - Keeps ACL-specific code out of `App.xaml.cs` and `FluxoDbContextFactory`.
- Modify `Fluxo.Data/Context/FluxoDbContextFactory.cs`
  - Exposes the machine-wide database directory and routes directory creation through the ACL preparer.
- Modify `Fluxo/App.xaml.cs`
  - Continues to call `FluxoDbContextFactory.EnsureDatabaseDirectoryExists()` before backup/migration; no direct ACL logic belongs here.
- Modify `Fluxo.Resources/CustomControls/FluxoMessageBox.cs`
  - Resolves safe dialog owners without assigning a dialog as its own owner.
- Modify `Fluxo.Installer.Msi/Folders.wxs`
  - Declares `CommonAppDataFolder\fluxo` for MSI creation.
- Modify `Fluxo.Installer.Msi/Package.wxs`
  - Schedules an elevated ACL repair custom action for install/repair.
- Create or modify `Fluxo.Tests/Infrastructure/MachineWideDataDirectoryPreparerTests.cs`
  - Verifies ACL helper semantics on a temp directory.
- Modify `Fluxo.Tests/Installer/InstallerMsiAuthoringTests.cs`
  - Verifies ProgramData folder authoring and ACL repair action.
- Create or modify `Fluxo.Tests/Views/Popups/FluxoMessageBoxOwnerTests.cs`
  - Verifies self-owner resolution is blocked.

---

### Task 1: Add ProgramData ACL Helper

**Files:**
- Create: `Fluxo/Infrastructure/MachineWideDataDirectoryPreparer.cs`
- Test: `Fluxo.Tests/Infrastructure/MachineWideDataDirectoryPreparerTests.cs`

- [ ] **Step 1: Write the failing ACL helper tests**

Create `Fluxo.Tests/Infrastructure/MachineWideDataDirectoryPreparerTests.cs`:

```csharp
using System.Security.AccessControl;
using System.Security.Principal;
using Fluxo.Infrastructure;
using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class MachineWideDataDirectoryPreparerTests
{
    [Fact]
    public void Prepare_GrantsUsersModifyAccessToDirectoryAndExistingRuntimeFiles()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var directory = Path.Combine(Path.GetTempPath(), $"fluxo-acl-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var dbPath = Path.Combine(directory, "fluxo.db");
        var walPath = Path.Combine(directory, "fluxo.db-wal");
        File.WriteAllText(dbPath, string.Empty);
        File.WriteAllText(walPath, string.Empty);

        try
        {
            MachineWideDataDirectoryPreparer.Prepare(directory);

            var directorySecurity = new DirectoryInfo(directory).GetAccessControl();
            Assert.Contains(
                GetUsersRules(directorySecurity),
                rule => HasModify(rule)
                        && rule.InheritanceFlags.HasFlag(InheritanceFlags.ContainerInherit)
                        && rule.InheritanceFlags.HasFlag(InheritanceFlags.ObjectInherit));

            foreach (var path in new[] { dbPath, walPath })
            {
                var fileSecurity = new FileInfo(path).GetAccessControl();
                Assert.Contains(GetUsersRules(fileSecurity), rule => HasModify(rule));
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

    private static IEnumerable<FileSystemAccessRule> GetUsersRules(FileSystemSecurity security)
    {
        var usersSid = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        return security
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .OfType<FileSystemAccessRule>()
            .Where(rule => usersSid.Equals(rule.IdentityReference)
                           && rule.AccessControlType == AccessControlType.Allow);
    }

    private static bool HasModify(FileSystemAccessRule rule) =>
        (rule.FileSystemRights & FileSystemRights.Modify) == FileSystemRights.Modify;
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~MachineWideDataDirectoryPreparerTests"
```

Expected: FAIL because `MachineWideDataDirectoryPreparer` does not exist.

- [ ] **Step 3: Implement the helper**

Create `Fluxo/Infrastructure/MachineWideDataDirectoryPreparer.cs`:

```csharp
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
```

- [ ] **Step 4: Run the focused tests and verify they pass**

Run:

```powershell
dotnet test Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~MachineWideDataDirectoryPreparerTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Fluxo\Infrastructure\MachineWideDataDirectoryPreparer.cs Fluxo.Tests\Infrastructure\MachineWideDataDirectoryPreparerTests.cs
git commit -m "fix: prepare machine-wide data directory ACLs"
```

---

### Task 2: Wire ACL Preparation Into Database Startup

**Files:**
- Modify: `Fluxo.Data/Context/FluxoDbContextFactory.cs`
- Test: `Fluxo.Tests/Infrastructure/MachineWideDataDirectoryPreparerTests.cs`

- [ ] **Step 1: Add a failing factory-level test**

Append this test to `MachineWideDataDirectoryPreparerTests`:

```csharp
[Fact]
public void DatabaseDirectoryPath_IsTheParentOfMachineWideDatabasePath()
{
    var databasePath = Fluxo.Data.Context.FluxoDbContextFactory.GetDatabasePath();
    var directoryPath = Fluxo.Data.Context.FluxoDbContextFactory.GetDatabaseDirectoryPath();

    Assert.Equal(Path.GetDirectoryName(databasePath), directoryPath);
    Assert.EndsWith(Path.Combine("fluxo", "fluxo.db"), databasePath, StringComparison.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Run the focused test and verify it fails**

Run:

```powershell
dotnet test Fluxo.Tests\Fluxo.Tests.csproj --filter "DatabaseDirectoryPath_IsTheParentOfMachineWideDatabasePath"
```

Expected: FAIL because `GetDatabaseDirectoryPath()` does not exist.

- [ ] **Step 3: Update the database factory**

Modify `Fluxo.Data/Context/FluxoDbContextFactory.cs`:

```csharp
using System.IO;
using Fluxo.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fluxo.Data.Context;

public sealed class FluxoDbContextFactory : IDesignTimeDbContextFactory<FluxoDbContext>
{
    public FluxoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FluxoDbContext>();
        optionsBuilder.UseSqlite(
            BuildConnectionString(),
            sqliteOptions => sqliteOptions.MigrationsAssembly("Fluxo"));

        return new FluxoDbContext(optionsBuilder.Options);
    }

    public static string BuildConnectionString()
    {
        var databasePath = GetDatabasePath();
        return $"Data Source={databasePath}";
    }

    public static string GetDatabasePath()
    {
        return Path.Combine(GetDatabaseDirectoryPath(), "fluxo.db");
    }

    public static string GetDatabaseDirectoryPath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(appDataPath, "fluxo");
    }

    /// <summary>
    /// Ensures the machine-wide database directory exists and can be modified by standard users.
    /// SQLite does not create parent folders.
    /// </summary>
    public static void EnsureDatabaseDirectoryExists()
    {
        MachineWideDataDirectoryPreparer.Prepare(GetDatabaseDirectoryPath());
    }
}
```

- [ ] **Step 4: Run the focused tests**

Run:

```powershell
dotnet test Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~MachineWideDataDirectoryPreparerTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Fluxo.Data\Context\FluxoDbContextFactory.cs Fluxo.Tests\Infrastructure\MachineWideDataDirectoryPreparerTests.cs
git commit -m "fix: prepare database directory before startup migration"
```

---

### Task 3: Add MSI ProgramData ACL Repair

**Files:**
- Modify: `Fluxo.Installer.Msi/Folders.wxs`
- Modify: `Fluxo.Installer.Msi/Package.wxs`
- Modify: `Fluxo.Tests/Installer/InstallerMsiAuthoringTests.cs`

- [ ] **Step 1: Add failing MSI authoring tests**

Append to `Fluxo.Tests/Installer/InstallerMsiAuthoringTests.cs`:

```csharp
[Fact]
public void Folders_DeclaresMachineWideProgramDataFolder()
{
    var wxs = File.ReadAllText(Path.Combine(
        GetRepositoryRoot(),
        "Fluxo.Installer.Msi",
        "Folders.wxs"));

    Assert.Contains("CommonAppDataFolder", wxs);
    Assert.Contains("FLUXOPROGRAMDATAFOLDER", wxs);
    Assert.Contains("Name=\"fluxo\"", wxs);
    Assert.Contains("CreateFolder", wxs);
}

[Fact]
public void Package_RepairsProgramDataAclDuringInstallAndRepair()
{
    var wxs = File.ReadAllText(Path.Combine(
        GetRepositoryRoot(),
        "Fluxo.Installer.Msi",
        "Package.wxs"));

    Assert.Contains("RepairFluxoProgramDataAcl", wxs);
    Assert.Contains("icacls.exe", wxs);
    Assert.Contains("*S-1-5-32-545:(OI)(CI)M", wxs);
    Assert.Contains("Execute=\"deferred\"", wxs);
    Assert.Contains("Impersonate=\"no\"", wxs);
    Assert.Contains("NOT REMOVE~=&quot;ALL&quot;", wxs);
}
```

- [ ] **Step 2: Run MSI authoring tests and verify they fail**

Run:

```powershell
dotnet test Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~InstallerMsiAuthoringTests"
```

Expected: FAIL because the ProgramData folder and ACL repair action are not authored yet.

- [ ] **Step 3: Author the ProgramData folder**

Modify `Fluxo.Installer.Msi/Folders.wxs`:

```xml
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
  <Fragment>
    <StandardDirectory Id="ProgramFiles64Folder">
      <Directory Id="INSTALLFOLDER" Name="fluxo" />
    </StandardDirectory>
  </Fragment>

  <Fragment>
    <StandardDirectory Id="CommonAppDataFolder">
      <Directory Id="FLUXOPROGRAMDATAFOLDER" Name="fluxo">
        <Component Id="FluxoProgramDataFolderComponent" Guid="B1F116D7-78E8-46D1-9037-8DAF2470A806" Bitness="always64">
          <CreateFolder />
        </Component>
      </Directory>
    </StandardDirectory>
  </Fragment>
</Wix>
```

- [ ] **Step 4: Include the ProgramData component in the feature**

Modify `Fluxo.Installer.Msi/Package.wxs` inside `Feature`:

```xml
<Feature Id="MainFeature" Title="fluxo" Level="1">
  <ComponentGroupRef Id="FluxoAppFiles" />
  <ComponentRef Id="FluxoProgramDataFolderComponent" />
</Feature>
```

- [ ] **Step 5: Schedule elevated ACL repair**

In `Fluxo.Installer.Msi/Package.wxs`, add this action to `InstallExecuteSequence`:

```xml
<Custom Action="RepairFluxoProgramDataAcl" After="InstallFiles" Condition="NOT REMOVE~=&quot;ALL&quot;" />
```

Add this custom action fragment next to the existing uninstall registry cleanup custom action:

```xml
<CustomAction
    Id="RepairFluxoProgramDataAcl"
    Directory="System64Folder"
    Execute="deferred"
    Impersonate="no"
    ExeCommand="cmd.exe /d /c &quot;icacls.exe &quot;&quot;%ProgramData%\fluxo&quot;&quot; /grant *S-1-5-32-545:(OI)(CI)M /T /C&quot;"
    Return="check" />
```

The `*S-1-5-32-545` SID targets the built-in Users group without depending on localized group names. `(OI)(CI)M` grants modify rights to the directory and inherited child objects.

- [ ] **Step 6: Run MSI authoring tests**

Run:

```powershell
dotnet test Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~InstallerMsiAuthoringTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```powershell
git add Fluxo.Installer.Msi\Folders.wxs Fluxo.Installer.Msi\Package.wxs Fluxo.Tests\Installer\InstallerMsiAuthoringTests.cs
git commit -m "fix: repair ProgramData ACLs during install"
```

---

### Task 4: Guard Message Box Owner Resolution

**Files:**
- Modify: `Fluxo.Resources/CustomControls/FluxoMessageBox.cs`
- Test: `Fluxo.Tests/Views/Popups/FluxoMessageBoxOwnerTests.cs`

- [ ] **Step 1: Write failing owner-resolution tests**

Create `Fluxo.Tests/Views/Popups/FluxoMessageBoxOwnerTests.cs`:

```csharp
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using Fluxo.Resources.CustomControls;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class FluxoMessageBoxOwnerTests
{
    [Fact]
    public void ResolveOwnerForDialog_ReturnsNullWhenResolvedOwnerIsDialogItself()
    {
        RunInSta(() =>
        {
            var dialog = new Window();

            var owner = FluxoMessageBox.ResolveOwnerForDialog(dialog, dialog, () => null);

            Assert.Null(owner);
        });
    }

    [Fact]
    public void ResolveOwnerForDialog_UsesFallbackOwnerWhenRequestedOwnerIsNull()
    {
        RunInSta(() =>
        {
            var dialog = new Window();
            var fallback = new Window();

            var owner = FluxoMessageBox.ResolveOwnerForDialog(dialog, null, () => fallback);

            Assert.Same(fallback, owner);
        });
    }

    private static void RunInSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
```

- [ ] **Step 2: Run the focused tests and verify they fail**

Run:

```powershell
dotnet test Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~FluxoMessageBoxOwnerTests"
```

Expected: FAIL because `ResolveOwnerForDialog` does not exist.

- [ ] **Step 3: Update message box owner resolution**

Modify `Fluxo.Resources/CustomControls/FluxoMessageBox.cs`:

```csharp
using System.Windows;
using Fluxo.Resources.Components;

namespace Fluxo.Resources.CustomControls;

public static class FluxoMessageBox
{
    public static MessageBoxResult Show(Window? owner, string message, string title,
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
    {
        var dialog = new MessageBoxPopup(message, title, buttons, icon);
        var resolvedOwner = ResolveOwnerForDialog(dialog, owner, GetActiveWindow);
        if (resolvedOwner is not null)
            dialog.Owner = resolvedOwner;

        dialog.ShowDialog();
        return dialog.Result;
    }

    internal static Window? ResolveOwnerForDialog(
        Window dialog,
        Window? requestedOwner,
        Func<Window?> fallbackOwnerResolver)
    {
        ArgumentNullException.ThrowIfNull(dialog);
        ArgumentNullException.ThrowIfNull(fallbackOwnerResolver);

        var resolvedOwner = requestedOwner ?? fallbackOwnerResolver();
        return ReferenceEquals(resolvedOwner, dialog) ? null : resolvedOwner;
    }

    private static Window? GetActiveWindow()
    {
        return Application.Current?.Windows
                   .OfType<Window>()
                   .FirstOrDefault(window => window.IsActive)
               ?? Application.Current?.MainWindow;
    }
}
```

- [ ] **Step 4: Run the focused tests**

Run:

```powershell
dotnet test Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~FluxoMessageBoxOwnerTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add Fluxo.Resources\CustomControls\FluxoMessageBox.cs Fluxo.Tests\Views\Popups\FluxoMessageBoxOwnerTests.cs
git commit -m "fix: avoid self-owned startup error dialogs"
```

---

### Task 5: Full Verification

**Files:**
- No planned source edits.

- [ ] **Step 1: Run the full test suite**

Run:

```powershell
dotnet test Fluxo.Tests\Fluxo.Tests.csproj
```

Expected: PASS.

- [ ] **Step 2: Build the app and installer**

Run:

```powershell
dotnet build Fluxo.slnx -c Release
dotnet build Fluxo.Installer.Msi\Fluxo.Installer.Msi.wixproj -c Release
dotnet build Fluxo.Installer.Bundle\Fluxo.Installer.Bundle.wixproj -c Release
```

Expected: all builds PASS.

If a build fails because `fluxo.exe` is running, stop and ask for confirmation before terminating the process. If approved, terminate that process and run the failed build again.

- [ ] **Step 3: Inspect installed-launch evidence if available**

Run:

```powershell
Get-WinEvent -FilterHashtable @{LogName='Application'; StartTime=(Get-Date).AddMinutes(-30)} |
  Where-Object { $_.ProviderName -in @('Application Error','.NET Runtime') -and ($_.Message -match 'fluxo|Fluxo') } |
  Select-Object TimeCreated,ProviderName,Id,Message
```

Expected: no new startup crash entries after installing or repairing with the new MSI.

- [ ] **Step 4: Commit verification notes only if source changed during verification**

If no files changed, do not commit. If tests or build fixes required source changes, commit only those files with a message that describes the fix.

---

## Plan Self-Review

- Spec coverage: installer ACL authority, defensive app startup preparation, existing install repair, machine-wide storage, and message-box guard are each mapped to tasks.
- Placeholder scan: no placeholder markers or incomplete instructions remain.
- Type consistency: `MachineWideDataDirectoryPreparer`, `GetDatabaseDirectoryPath`, and `ResolveOwnerForDialog` names are consistent across tests and implementation steps.

## Reference

- Microsoft `icacls` command reference: https://learn.microsoft.com/windows-server/administration/windows-commands/icacls
