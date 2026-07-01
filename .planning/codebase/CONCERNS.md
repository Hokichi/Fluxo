# Codebase Map: Concerns

Generated: 2026-07-01
Branch: `main`

## Current Health

- `dotnet test Fluxo.Tests/Fluxo.Tests.csproj --no-build --no-restore` currently reports **4 failed, 1,375 passed, 0 skipped**.
- Build/test emits `NU1903` for transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (known high-severity advisory `GHSA-2m69-gcr7-jv3q`) across the application, data, services, resources, installer, and test projects.
- Compiler warnings remain in production code, including possible null dereferences/arguments in `Fluxo/ViewModels/Shell/Main/LedgerVM.cs:1157`, `Fluxo/ViewModels/Popups/AddAccountVM.cs:490`, `Fluxo/ViewModels/Popups/Planning/PlanningReportVM.cs:382`, and `Fluxo/Views/Shell/Main/MainWindow.xaml.cs:2038`.
- Additional nullability warnings exist in `Fluxo.Core/Entities/Account.cs`, `SavingGoal.cs`, `Tag.cs`, `Fluxo/ViewModels/Controls/DayOfWeekVM.cs`, `Fluxo.Resources/Components/FluxoWave.xaml.cs`, and `Fluxo.Resources/CustomControls/FadingScrollViewer.cs`.

## Failing Tests

- `Fluxo.Tests/Views/Popups/Settings/SettingsDebtIousTabLayoutTests.cs:36` expects a removed `AmountSign` XAML fragment.
- `Fluxo.Tests/ViewModels/Shell/Main/NotificationPanelVMTests.cs:670` cannot find the expected late-payment repayment item.
- `Fluxo.Tests/Views/CustomControls/BalloonToggleTests.cs:264` expects a 300 ms long-press threshold while production uses 500 ms.
- `Fluxo.Tests/ViewModels/Popups/Settings/SettingsTagsTabTests.cs:21` expects a two-column layout no longer present.
- Three failures are source/layout contract assertions and may be stale expectations; the notification failure exercises behavior and needs root-cause triage before treating the suite as a clean regression gate.

## June 2026 Transaction Migration Risk

- `Fluxo/Migrations/20260627151726_UnifyTransactionsAndTags.cs` performs the largest recent data rewrite: legacy expenses, expense logs, and income logs are copied into `Transactions`, tags are renamed, and old tables are removed.
- Follow-up migrations `20260627220351_EnsureSystemTagsAfterTransactionMigration.cs`, `20260628021233_AddTransactionLoggedOn.cs`, and `20260630032006_AddRecurringTransactionEndDate.cs` repair/extend that schema. This sequence must remain ordered and tested as one upgrade path.
- `Fluxo.Tests/Infrastructure/AppDatabaseMigrationTests.cs` validates creation of a fresh current database. It does **not** construct a populated pre-unification database and prove preservation of legacy rows, parent links, tags, account links, IOU flags, and system tags through the full upgrade.
- `Fluxo/App.xaml.cs` infers schema age from SQLite tables/columns, rewrites legacy migration-history IDs, toggles `PRAGMA foreign_keys`, and can seed `__EFMigrationsHistory`. New migrations can invalidate these hand-maintained assumptions even when a fresh-database test passes.
- Fresh databases use `EnsureCreatedAsync` and then mark all migrations applied in `Fluxo/App.xaml.cs`; migration-only seed/data transforms must therefore also be represented by `SeedCurrentSchemaDataAsync` or fresh and upgraded databases diverge.

## Backup And Restore Data-Loss Risk

- `Fluxo.Services/Backups/UserBackupService.cs:354` creates safety backups by copying the live SQLite main file with `FileShare.ReadWrite`; it does not use SQLite's online backup API and does not capture WAL/SHM sidecars. Under an open/WAL-backed connection, the copied file may omit committed pages.
- Append/overwrite recovery replaces the database file after failures (`UserBackupService.cs:367`) while the injected data service/context may still hold tracked entities and an open connection. Tests only prove byte-copy replacement, not rollback correctness with a live EF context.
- Restore is not one explicit database transaction. `AppendTransactionsAsync` saves once to obtain parent IDs (`UserBackupService.cs:1026`) before the outer append/overwrite save, so partial persistence relies on the file-level safety restore.
- Startup database backups in `Fluxo/App.xaml.cs` are pruned after three days. They protect short-term upgrades but are not durable user backup policy.
- Current backup schema intentionally rejects older schema versions in `UserBackupService.cs`; `FluxoUserBackupDocument.cs` still contains ignored legacy expense/log properties, so compatibility intent is easy to misread. Keep version-rejection and legacy-field behavior covered whenever the transaction document changes.

## Update And Installer Trust Boundary

- `Fluxo/Services/Updates/AppUpdateService.cs` accepts the first GitHub release asset matching `fluxo-*-Installer.exe`, downloads it, and returns its temp path without checksum, signature, publisher, or Authenticode verification.
- `Fluxo/App.xaml.cs:292` executes that downloaded installer with `UseShellExecute = true` through `Fluxo/Services/Updates/AppUpdateInstallerLauncher.cs`. HTTPS and GitHub repository control are therefore the only integrity boundary.
- Download URL validation requires only an absolute URI; it does not restrict scheme or host when `DownloadInstallerAsync` is called independently.
- Installer registry/runtime discovery contains broad empty catches in `Fluxo.Installer/BootstrapperEntry.cs`, `Services/InstalledVersionRegistryReader.cs`, `DotNetRuntimeDetector.cs`, and `DotNetRuntimeInstaller.cs`. Some are best-effort probes, but failures become indistinguishable from “not installed” and can trigger incorrect repair/install decisions.

## High-Change, High-Coupling Areas

- `Fluxo/ViewModels/Popups/AddNewTransactionVM.cs` is about 2,706 lines and gained BNPL/installment, IOU, repayment, duplicate detection, history, split, goal, and recurring behavior during June. Shared state and validation changes have broad financial effects.
- `Fluxo/Views/Shell/Main/MainWindow.xaml.cs` is about 2,398 lines and owns navigation, date-range handoffs, popups, shortcuts, window state, tray behavior, animation, and search. Recent helper extraction reduced some local algorithms but not orchestration coupling.
- `Fluxo/ViewModels/Shell/Main/LedgerVM.cs` is about 1,561 lines and combines query/filter/grouping, hierarchy, edits, batch actions, CSV export, and navigation-range state.
- `Fluxo/App.xaml.cs` is about 1,380 lines and orders backup, migration, period synchronization, logging, DI, single-instance, tray, update, and exception handling. Its `async void OnStartup` boundary makes complete catch/log behavior essential.
- `Fluxo.Installer/ViewModels/InstallerViewModel.cs` is about 1,488 lines and coordinates elevation, runtime installation/removal, MSI state, registry ownership, process handling, and UI state.

## Persistence And Query Fragility

- `Fluxo.Data/UnitOfWork.cs` exposes repositories plus `SaveChangesAsync` but no explicit transaction API. Multi-step financial operations depend on one EF save being atomic or manually compensate across multiple saves.
- `Fluxo.Data/Repositories/Repository.cs` manually resolves tracked-entity conflicts during update/remove; changes to tracking behavior can surface as stale values or duplicate tracked instances.
- `Fluxo.Data/Context/FluxoDbContext.cs` globally auto-includes non-collection reference navigations. This simplifies callers but silently increases object graphs and query cost as relationships are added.
- Recent recurring-expiration filtering is covered in `Fluxo.Tests/Infrastructure/RecurringTransactionRepositoryTests.cs`; preserve tracked-row and boundary-date cases when changing the global query filter.

## WPF Binding And Converter Semantics

- `ConvertBack` throws in many files under `Fluxo.Resources/Converters/`, and in the ledger tag-name converter. These are **intentional one-way converter contracts**, not unfinished feature stubs.
- `BackgroundToForegroundConverter` explicitly uses `NotSupportedException` and has tests for both single- and multi-value `ConvertBack` paths.
- `NotImplementedException` in other converter `ConvertBack` methods is still runtime-fragile if XAML defaults to two-way binding. Bindings that consume read-only properties or one-way converters must specify `Mode=OneWay` per project convention.
- Real gaps should be identified from an actual writable/two-way callsite, not from exception type alone.

## Test Coverage Shape

- The suite is broad (176 files containing facts/theories), with strong view-model, repository, backup mapping, and source-contract coverage.
- Many UI tests under `Fluxo.Tests/Views/` read XAML/C# as text and assert fragments. They catch contract drift cheaply but do not execute WPF binding resolution, template application, dispatcher timing, animation, focus, or popup behavior.
- No end-to-end populated legacy database fixture validates the June unified-transaction migration.
- Backup safety tests do not cover WAL mode, concurrent/open connections, forced mid-operation failure, EF tracker refresh, or post-rollback continued use.
- Update tests cover streaming, cleanup, versions, and asset naming, but absence of integrity verification is production behavior rather than a tested security guarantee.

## Workspace And Build Fragility

- `Fluxo/Fluxo.csproj` reorganizes build/publish DLLs with PowerShell, file moves, and `cmd /c mklink /H`; behavior depends on Windows filesystem permissions and hard-link support.
- Root `msbuild.binlog` and `fluxo-tests.binlog` are local artifacts. Build outputs, databases, and other ignored artifacts must remain uncommitted.
- A running app or test executable can lock build output. Project instruction requires terminating that executable and retrying the build/test.

## Prioritized Follow-Ups

1. Clear the four current test failures, starting with behavioral `NotificationPanelVMTests`; restore a trustworthy green baseline.
2. Upgrade/override the SQLite native package chain to remove `GHSA-2m69-gcr7-jv3q`, then rerun the full suite and installer smoke path.
3. Add one populated pre-`20260627151726` SQLite fixture and assert row counts, relationships, flags, system tags, and values after all migrations.
4. Replace live-database file copy rollback with SQLite online backup or an explicit EF/SQLite transaction; test failure recovery with WAL and open contexts.
5. Verify update artifacts before execution (pinned release host plus signed/checksummed artifact).
6. Resolve production nullable warnings at their source, prioritizing `LedgerVM.cs:1157` and `AddAccountVM.cs:490`.
7. Split high-change classes only along already-visible seams when touched; avoid speculative framework layers.
