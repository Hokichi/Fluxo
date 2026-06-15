# Codebase Map: Concerns

Generated: 2026-06-15

## Summary

The codebase has healthy coverage and clear project boundaries, but several areas deserve caution: startup complexity, EF migration compatibility logic, WPF code-behind size, installer/runtime side effects, and local financial-data safety.

## Startup Complexity

- `Fluxo/App.xaml.cs` is large and owns many responsibilities: DI bootstrap, migration, backup, logging, single-instance handling, tray behavior, first-run wizard, update shutdown, and global exception handlers.
- Startup order is important. Changes around `BackupDatabaseOnStartupAsync`, `MigrateDatabaseAsync`, `SyncBudgetAllocationPeriodAsync`, and `InitializeLoggingAsync` should be tested together.
- The app uses `async void OnStartup`, so startup failures must remain carefully caught and logged.

## Migration Fragility

- EF migrations live in `Fluxo/Migrations/`.
- `Fluxo/App.xaml.cs` contains fallback logic to infer old schema state and seed migration history.
- This compatibility path reads SQLite metadata manually through `PRAGMA table_info`.
- Any migration that changes old-table names, column names, or baseline assumptions can break existing-user upgrades.
- User instruction says if a migration is created, run that migration.

## Repository Tracking Semantics

- `Fluxo.Data/Repositories/Repository.cs` manually handles tracked entity conflicts for update/remove.
- Most reads are no-tracking, but some write flows reattach or modify entities.
- Changes to navigation auto-includes in `Fluxo.Data/Context/FluxoDbContext.cs` can affect query size and object graphs.
- `ConfigureReferenceAutoIncludes` enables all non-collection reference navigations globally.

## WPF Code-Behind Size

- `Fluxo/Views/Shell/Main/MainWindow.xaml.cs` is large and manages navigation, popups, keyboard shortcuts, layout transitions, drag/maximize behavior, search, and history.
- `Fluxo/App.xaml.cs` and `Fluxo.Installer/ViewModels/InstallerViewModel.cs` are also high-complexity files.
- Small behavior changes in these files can have broad UI side effects.

## Converter Limitations

- Many WPF converters throw `NotImplementedException` from `ConvertBack`, including files under `Fluxo.Resources/Converters/`.
- This is normal for one-way converters, but accidental two-way bindings can fail at runtime.
- Examples include `BoolToVisibilityConverter`, `MoneyDisplayConverter`, `ObjectToNullConverter`, and `ProgressToArcGeometryConverter`.

## Installer And Registry Side Effects

- Installer flow touches machine registry keys and process state in `Fluxo.Installer/ViewModels/InstallerViewModel.cs`.
- Runtime ownership writes registry state in `Fluxo.Installer/Services/DotNetRuntimeOwnershipStore.cs`.
- Build or installer failures can be caused by a running executable; project AGENTS instructions require asking before terminating that process.

## Update Flow Safety

- `Fluxo/Services/Updates/AppUpdateService.cs` downloads installer executables from GitHub release assets.
- Asset name is validated, but release trust depends on the GitHub repository and release process.
- `LaunchUpdateInstallerAndShutdown` in `Fluxo/App.xaml.cs` starts an installer then shuts down the app.

## Financial Data Safety

- Local database path is `%LocalAppData%\fluxo\fluxo.db`.
- Startup backup retention is short at three days.
- Delete-all-data and restoration flows should keep tests around overwrite, path traversal, and restoration safety current.

## Documentation Drift

- README image alt text still says placeholder for several images.
- README mentions local-first behavior and current app capabilities; changes to integrations or sync need doc updates.

## Build Artifacts In Workspace

- Root contains binary logs such as `msbuild.binlog` and `fluxo-tests.binlog`.
- Build output folders and SQLite DBs are ignored; do not force commit ignored generated files.

## Immediate Watchpoints

- Do not refactor `App.xaml.cs`, `MainWindow.xaml.cs`, or installer state logic without focused regression tests.
- When touching migrations, validate existing database upgrade paths, not only fresh database creation.
- When changing update or installer code, verify GitHub release naming and WiX packaging assumptions.
