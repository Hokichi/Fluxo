# Codebase Concerns

**Analysis Date:** 2026-05-09  
**Focus:** concerns, risks, and follow-up investigation areas

This refresh is based on read-only inspection of project files, representative source/tests, file sizes, and `TODO`/`FIXME` style searches. No build or test run was performed.

## Executive Risk Summary

- The largest current risks are persistence location/migration behavior, installer/build coupling, and large UI/view-model classes with many responsibilities.
- Test coverage has grown around installer, notification, and dashboard behavior, but several high-risk data and transaction paths still appear thin.
- Several older concerns have improved: startup now calls database migration and expense-log post-termination cleanup from `Fluxo/App.xaml.cs`.

## Observed Evidence

### Large Change Surfaces

- `Fluxo/Views/Shell/Main/MainWindow.xaml.cs` is about 1,232 lines.
- `Fluxo.Installer/ViewModels/InstallerViewModel.cs` is about 1,159 lines.
- `Fluxo/ViewModels/Shell/Main/BudgetAllocationPanelVM.cs` is about 937 lines.
- `Fluxo/ViewModels/Shell/Main/NotificationPanelVM.cs` is about 932 lines.
- `Fluxo/App.xaml.cs` is about 831 lines.
- Large XAML/style files include `Fluxo.Resources/Resources/Styles/ButtonStyles.xaml` at about 1,284 lines and `Fluxo/Views/Shell/Main/MainWindow.xaml` at about 785 lines.

### Persistence and Migrations

- Runtime startup now calls `MigrateDatabaseAsync(...)` in `Fluxo/App.xaml.cs`.
- Startup also calls `ExpenseLogService.PostTerminationCleanupAsync(...)`.
- The SQLite database path is `Path.Combine(AppContext.BaseDirectory, "fluxo.db")` in `Fluxo.Data/Context/FluxoDbContextFactory.cs`.
- EF migrations live in `Fluxo/Migrations/**`, while the context and repository code live in `Fluxo.Data/**`; data registration explicitly uses `sqliteOptions.MigrationsAssembly("Fluxo")`.
- `Fluxo/App.xaml.cs` contains schema-inference code that can seed `__EFMigrationsHistory` when an existing database has app tables but no migration history.

### Data Access and Scaling

- Repository `GetAllAsync` generally returns full lists with `AsNoTracking()`.
- `Fluxo.Data/Context/FluxoDbContext.cs` auto-includes all reference navigations globally.
- `AnalyticsService.GetAnalyticsAsync(...)` loads all expense logs, income logs, and goals before filtering/aggregating in memory.
- `NotificationPanelVM` loads full expenses/logs/sources/goals and synchronizes persisted notifications during refresh.
- Several UI flows mutate data through `IAppDataService`/unit-of-work-style access directly from popup or shell view models.

### Build, Packaging, and Platform

- Projects target `net10.0-windows` and the installer uses WiX 7 projects.
- The app project moves first-party DLLs into `libs/`, vendor DLLs into `vendor/`, and creates a hardlink for the root executable after build.
- `AssemblyResolutionBootstrap` resolves assemblies from `libs`, `vendor`, and `plugins`.
- `Fluxo.Installer.Msi/Fluxo.Installer.Msi.wixproj` builds the WPF app as a pre-build step for the MSI.
- No repo-root `global.json`, `.editorconfig`, `Directory.Build.props`, `Directory.Packages.props`, `NuGet.config`, `.runsettings`, or `.github/workflows` files were detected.

### UI and Resource Handling

- Shared resources are split into a dedicated `Fluxo.Resources` project, but some style files remain very large.
- Multiple converter `ConvertBack` methods throw `NotImplementedException` under `Fluxo.Resources/Converters/**`.
- UI code contains many `async void` event handlers and dispatcher/timer interactions, especially in popup code-behind and the startup wizard.

### Test Coverage Snapshot

- Current inventory found about 64 test `.cs` files and about 348 source `.cs` files.
- Stronger observed coverage areas:
  - Installer flow state, launch, registry version, operation mode, and MSI authoring checks.
  - Notification grouping/action/summary behavior.
  - Dashboard panel slices and several view layout/static-structure checks.
  - Data operation runner/lifetime registration checks.
- Thin or absent direct coverage was observed for:
  - `App.xaml.cs` migration-history inference and first-run startup sequence.
  - Real `AnalyticsService` database/query behavior.
  - `ExpenseService`, `SpendingSourceService`, and `TagService` mutation edge cases.
  - The full `LogMemoryActions` undo/redo matrix as a dedicated suite.
  - Packaged app smoke behavior after the custom `libs`/`vendor` output layout.

## Inferred Risks

### Data Location and User Permissions

- Because `fluxo.db` is under `AppContext.BaseDirectory`, an installed app under `Program Files` may write user data into the install directory.
- Practical risks:
  - Normal users may hit write-permission failures.
  - User data may be coupled to repair/uninstall/upgrade operations.
  - Multiple Windows users on one machine may share or contend over one install-local database.
- Follow-up: confirm installed runtime location and decide whether data/logs should move to `%LocalAppData%` or another per-user application-data folder.

### Migration Recovery Complexity

- The manual migration-history inference in `App.xaml.cs` reduces startup failures for existing databases, but it is also a high-risk compatibility surface.
- Practical risks:
  - A partially migrated or manually modified SQLite file could be inferred incorrectly.
  - Schema inference may need an update every time a migration changes detectable columns/tables.
  - Failures happen during startup, where recovery UX is limited.
- Follow-up: add focused tests around empty DBs, existing DBs with no `__EFMigrationsHistory`, partially migrated DBs, and corrupted/tampered DBs.

### Large UI/View-Model Classes

- The largest classes mix orchestration, UI state, data refresh, navigation, and mutation workflows.
- Practical risks:
  - Small feature edits have broad regression surfaces.
  - Async/timer/dispatcher ordering bugs are hard to reason about.
  - Review and test setup costs stay high.
- Follow-up: extract narrowly around stable seams first: notification synchronization, installer preflight/cleanup, main-window tray lifecycle, and budget-panel mutation helpers.

### Data Access Scaling

- Full-list reads plus global reference auto-includes are simple and testable, but may become costly as transaction history grows.
- Practical risks:
  - Analytics and dashboard refresh time can grow with all historical logs, not just the selected range.
  - Auto-includes may bring larger object graphs than each caller needs.
  - Notification refresh can reprocess broad datasets frequently.
- Follow-up: profile with synthetic large datasets before optimizing; then add repository/query methods for date-range and dashboard-specific reads.

### Installer and Output Layout Fragility

- The custom post-build output organization and WiX pre-build coupling are nonstandard.
- Practical risks:
  - Packaging may miss files if output layout changes.
  - Hardlink creation can behave differently across filesystems or permission contexts.
  - Assembly resolution failures may appear only in installed/published layouts.
- Follow-up: add an installer/package smoke checklist or automated test that verifies the installed executable starts and resolves assemblies from `libs`/`vendor`.

### UI Resource Regression Risk

- Very large shared style dictionaries and one-way converters with throwing `ConvertBack` methods are easy to break through binding changes.
- Practical risks:
  - A future two-way binding can surface runtime converter exceptions.
  - Style changes can have wide, hard-to-preview blast radius across windows/popups.
- Follow-up: keep existing XAML static tests, add targeted checks when changing converter bindings or shared resource dictionaries, and consider splitting the largest dictionaries by control family.

### Tooling Drift

- Missing repo-level SDK/config/CI files mean local machines can diverge on SDK selection, formatting, package restore behavior, and test expectations.
- Practical risks:
  - Net/WiX/package upgrades may appear as local-only failures.
  - Formatting/style drift can increase review noise.
- Follow-up: add `global.json`, `.editorconfig`, and a minimal CI workflow once the expected .NET 10 SDK and Windows runner requirements are settled.

## Practical Investigation Backlog

1. Verify installed app behavior with a standard non-admin user: database creation, log writing, startup registration, repair, and uninstall.
2. Add migration recovery tests around the schema-inference paths in `App.xaml.cs`.
3. Build a packaged-layout smoke test for the `libs`/`vendor` assembly resolution path.
4. Profile analytics/dashboard/notification refresh with large synthetic expense-log and income-log histories.
5. Add direct service tests for `AnalyticsService`, `ExpenseService`, `SpendingSourceService`, and `TagService`.
6. Add focused undo/redo tests for `LogMemoryActions`, starting with delete/edit/restore paths that affect balances and soft-deleted logs.
7. Audit converter usages before any binding-mode changes; either implement safe `ConvertBack` behavior or assert one-way usage.
8. Decide on repo-level tooling baselines: SDK pinning, formatting rules, and CI/test gate.

---

*Concerns analysis refreshed: 2026-05-09*
