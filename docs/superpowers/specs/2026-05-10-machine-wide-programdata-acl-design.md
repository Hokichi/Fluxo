# Machine-Wide ProgramData ACL Startup Fix Design

## Context

Fluxo is intended to use a machine-wide SQLite database at `C:\ProgramData\fluxo\fluxo.db`. Installed launches crash during startup when the app tries to migrate or repair that database as a standard user.

Windows Event Log shows the current failure chain:

- `Fluxo.Core.Exceptions.DataOperationException: Failed to migrate database`
- `System.UnauthorizedAccessException: Access to the path 'C:\ProgramData\fluxo\fluxo.db' is denied`
- earlier attempts also saw `SQLite Error 1: 'no such table: SpendingSources'`
- the startup error path can then throw `System.ArgumentException: Cannot set Owner property to itself`

The current folder ACL allows `BUILTIN\Users` to write to `C:\ProgramData\fluxo`, but the existing `fluxo.db` file only grants users read/execute. That prevents normal users from writing, replacing, or deleting the database file during EF Core startup recovery and migrations.

## Goal

Keep Fluxo's data machine-wide while allowing non-admin users to start the installed app, run migrations, and recover from empty or invalid machine-wide database files.

## Non-Goals

- Do not move the database to per-user app data.
- Do not require users to run Fluxo as administrator.
- Do not redesign the migration model beyond what is needed for reliable startup.
- Do not change installer UX except where required for repair/install correctness.

## Approach

Use a two-layer fix:

1. The installer creates and permissions `C:\ProgramData\fluxo` as shared mutable app data.
2. The app defensively checks the machine-wide data directory before SQLite startup work and repairs permissions when the current process is allowed to do so.

The installer remains the authority for ACLs because it runs elevated for per-machine installs. The app check covers development runs, repaired installs, and partially migrated installs, but it must not assume it can always repair ACLs without elevation.

## Installer Changes

The MSI should include a component for the machine-wide data directory. That component should:

- create `C:\ProgramData\fluxo`;
- grant `BUILTIN\Users` modify-level access to the directory;
- apply object/container inheritance so newly created `fluxo.db`, `fluxo.db-wal`, `fluxo.db-shm`, logs, and backups are writable by standard users;
- preserve administrator and system full control.

The installer should not package or install a seed `fluxo.db` into either the install folder or ProgramData. Database files remain runtime state, not install payload.

Repair should reapply the ACL so existing broken installs can be fixed by running the installed repairer or installer maintenance flow.

## App Startup Changes

Before `BackupDatabaseOnStartupAsync` and `MigrateDatabaseAsync`, Fluxo should prepare the machine-wide data location through a small focused helper. The helper should:

- ensure the ProgramData directory exists;
- attempt to grant users modify access to the directory and inherit it to children;
- attempt to repair ACLs on existing runtime files that match Fluxo database/log state;
- surface permission failures clearly through logging and the existing startup failure path.

If ACL repair fails because the app is not elevated, startup should fail with the underlying permission error logged. The installer repair flow is the recovery path for installed builds.

## Migration Behavior

`MigrateDatabaseAsync` currently treats an existing database with no EF migration history and no application tables as disposable, then calls `EnsureDeletedAsync` and `EnsureCreatedAsync`. That requires delete permission on `fluxo.db`.

With correct ACLs this path can work. Tests should lock in that the app prepares the machine-wide data path before migration so this delete/recreate path is not run against read-only ProgramData files.

## Error Dialog Guard

`FluxoMessageBox.Show(null, ...)` resolves the active window as owner. During startup failure, the active window can be the message box being created or another transient startup window, which can produce `Cannot set Owner property to itself`.

The message box owner resolution should avoid assigning a window as its own owner. If the resolved owner equals the dialog being shown, the dialog should show without an owner rather than crashing.

## Test Strategy

Add focused tests for:

- the ProgramData ACL helper grants modify/inheritance semantics to the Users group where testable without requiring global machine state;
- app startup invokes data-location preparation before database backup and migration work;
- WiX authoring includes a ProgramData component and permission grant for the machine-wide data directory;
- `FluxoMessageBox` does not set a dialog as its own owner when no safe owner exists.

Existing installer and packaging tests should continue to pass.

## Verification

Run:

- `dotnet test Fluxo.Tests\Fluxo.Tests.csproj`
- a Release installer build
- an installed app launch from a non-admin context when possible

If a build fails because `fluxo.exe` is running, ask before terminating the process, then rebuild after approval.
