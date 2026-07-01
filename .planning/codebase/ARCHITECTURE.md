# Codebase Map: Architecture

Generated: 2026-07-01

## Summary

Fluxo is a Windows-only, layered WPF desktop application. `Fluxo.Core` owns domain models and contracts; `Fluxo.Data` implements SQLite/EF Core persistence; `Fluxo.Services` implements reusable application services; `Fluxo.Resources` contains shared WPF controls and resources; and `Fluxo` is both composition root and presentation/application shell. Separate WiX projects build the managed installer UI, MSI, and Burn bundle.

## Project Dependency Direction

- `Fluxo.Core` is the base project. It contains entities, DTOs, enums, filters, budgeting rules, and repository/service/operation interfaces.
- `Fluxo.Data` references `Fluxo.Core` and implements its repository, unit-of-work, and operation-scope contracts with EF Core and SQLite.
- `Fluxo.Services` references `Fluxo.Core` and `Fluxo.Data`; it supplies persistence-facing services, backup/restore, logging, and DPAPI UI-lock protection.
- `Fluxo.Resources` references `Fluxo.Core` and `Fluxo.Services`; it supplies reusable WPF controls, components, converters, styles, and messenger message types.
- `Fluxo` references all four projects and contains the executable, dependency registration, migrations, app-only services, view models, and views.
- `Fluxo.Installer` references `Fluxo.Resources`; WiX MSI and bundle projects remain packaging boundaries outside the app runtime graph.
- `Fluxo.Tests` references the app, installer, and services projects and tests behavior across all layers.

## Domain and Persistence Boundaries

- Current persisted model is unified around `Account`, `Transaction`, `Tag`, `SavingGoal`, `RecurringTransaction`, `Notification`, `UserSettings`, and singleton `BudgetAllocation` entities in `Fluxo.Core/Entities/`.
- `Transaction` represents income, expense, repayment, transfer-related, split/parent, debt/IOU, pinned, and soft-deleted records; older expense/income log types survive only in migration compatibility code.
- Repository contracts live in `Fluxo.Core/Interfaces/Repositories/`; concrete implementations live in `Fluxo.Data/Repositories/`.
- `Fluxo.Data/UnitOfWork.cs` aggregates typed repositories and is the explicit `SaveChangesAsync` boundary.
- `Fluxo.Data/Context/FluxoDbContext.cs` owns table mapping, constraints, relationships, numeric money columns, date normalization, transaction timestamps, and reference auto-includes.
- The SQLite file is `%LOCALAPPDATA%/fluxo/fluxo.db`, resolved centrally by `Fluxo.Data/Context/FluxoDbContextFactory.cs`.
- EF migrations are compiled into the `fluxo` executable and stored under `Fluxo/Migrations/`, not in `Fluxo.Data`.

## Composition and Lifetimes

- `Fluxo/App.xaml.cs` manually creates a `ServiceCollection`, calls `AddFluxoData`, `AddFluxoPresentation`, and `AddUIData`, then builds the root provider.
- `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs` registers scoped `FluxoDbContext`, repositories, and `IUnitOfWork`; singleton `IDataOperationRunner` creates a fresh async scope per operation.
- `Fluxo/Extensions/ServiceCollectionExtensions.cs` registers AutoMapper profiles, application services, shell view models, pages, windows, and popups.
- Long-lived shell state (`MainVM`, dashboard panels, ledger, `MainWindow`) is singleton. Editors, settings tabs, pages, popups, and wizard state are mostly transient or resolved from explicit scopes.
- AutoMapper flow is entity -> DTO via `Fluxo.Services/Mappings/EntityDtoProfile.cs`, then DTO -> presentation model via `Fluxo/Mappings/DtoViewModelProfile.cs`.

## Application Startup Flow

1. `Fluxo.App` initializes bootstrap logging and global exception handlers in its constructor, then builds DI and resolves startup services.
2. `OnStartup` uses `SingleInstanceCoordinator` and `SingleInstanceStartupPolicy`; secondary launches signal the primary instance and exit.
3. The primary instance parses tray-start mode, creates the database directory, takes a best-effort startup backup, migrates SQLite, syncs the active budget-allocation period, and switches logging to the current username.
4. `StartupLoaderPopup` stays visible while first-run state, soft-deleted transaction cleanup, startup registration, update checks, and (for existing users) `MainVM` data initialization run.
5. First run resolves a scoped `QuickSetupWizard`, blocks with `ShowDialog`, and persists its draft through application services.
6. The singleton `MainWindow` is shown or hidden to the system tray. Tray startup may show a notification summary popup; later activation requests restore the existing window.
7. Shutdown disposes tray/single-instance resources. Explicit update launch delegates to the installer before process shutdown.

## Migration Strategy

- `App.MigrateDatabaseAsync` is the runtime migration entry point and executes inside `IDataOperationRunner`.
- A missing database is created directly from the current EF model, seeded, and given a complete migration-history table before normal `MigrateAsync` runs.
- Existing databases pass through legacy table/column normalization and migration-history remapping before pending migrations apply.
- Compatibility helpers in `Fluxo/App.xaml.cs` inspect SQLite schema directly because historical installs may predate current EF metadata.
- Startup backup precedes migration; failures are logged and startup aborts through the global startup error path.

## Read and Write Data Flow

1. XAML binds to a view model; code-behind handles window mechanics, navigation, popup ownership, and visual transitions.
2. A view model calls a service interface or `IDataOperationRunner` directly.
3. `DataOperationRunner` asks `DataOperationScopeFactory` for a new async DI scope and exposes its scoped `IUnitOfWork` through `DataOperationScope`.
4. A typed repository issues EF Core queries against `FluxoDbContext`. Read queries generally use no-tracking identity resolution; entity-specific repositories apply soft-delete and ordering rules.
5. Writes update repository state and explicitly call `UnitOfWork.SaveChangesAsync`.
6. Service paths map entities to DTOs, then presentation code maps or constructs WPF view models.
7. Unexpected operation failures are logged and wrapped in `DataOperationException`; cancellation and existing `DataOperationException` values pass through.

## Presentation and Control Flow

- `Fluxo/Views/Shell/Main/MainWindow.xaml` is the application shell; its code-behind owns page scope creation, navigation, shortcuts, header search, tray/window transitions, and popup launching.
- Main pages are `Dashboard`, `Analytics`, `Calendar`, and `Ledger` under `Fluxo/Views/Shell/Main/Pages/`; dashboard sections live beside them under `Sections/`.
- `Fluxo/ViewModels/Shell/Main/MainVM.cs` composes dashboard, ledger, day selection, user settings, and app-lock state. `DashboardVM` composes budget, spending, notification, saving-goal, and upcoming-event panels.
- Popup workflows live under paired `Fluxo/Views/Popups/` and `Fluxo/ViewModels/Popups/` trees; settings and data-management workflows create scoped services when an atomic editing session is required.
- CommunityToolkit.Mvvm provides observable state, commands, and `WeakReferenceMessenger`. Messages in `Fluxo.Resources/Resources/Messages/` decouple page navigation, date/filter changes, settings orchestration, and cross-panel refreshes.
- `Fluxo.Resources/CustomControls/BasePopup.cs` and related controls/styles provide the common popup and visual-control layer shared by app and installer.

## Installer and Packaging Flow

- `Fluxo.Installer/Program.cs` is the managed bootstrapper entry point and enforces one installer instance with a global mutex.
- `Fluxo.Installer/BootstrapperEntry.cs` adapts WiX Burn events into interactive WPF or headless execution, performs elevation relaunch, and drives detect/plan/apply completion.
- `Fluxo.Installer/ViewModels/InstallerViewModel.cs` is the installer UI state machine; views under `Fluxo.Installer/Views/Pages/` render each state.
- `Fluxo.Installer.Msi/Package.wxs` authors the per-machine MSI; `Fluxo.Installer.Bundle/Bundle.wxs` chains it with the managed bootstrapper.
- `Fluxo/Fluxo.csproj` contains build/publish targets that clean legacy artifacts and arrange first-party assemblies under `libs/` and vendor assemblies under `vendor/`.

## Failure and Coordination Patterns

- App-wide dispatcher, app-domain, and unobserved-task handlers log uncaught failures from `Fluxo/App.xaml.cs`.
- Startup stages are logged individually; startup failure shows `IDialogService`/`FluxoMessageBox` and shuts down.
- `IDataOperationRunner` centralizes scoped disposal, logging, and user-safe data-operation errors.
- Soft deletion is deliberate for transactions; startup invokes `ITransactionService.PostTerminationCleanupAsync` to physically remove terminated records.
- `WeakReferenceMessenger` is the main in-process event bus; direct property-change propagation is used inside composed shell view models.

## Key Entry Points

- App process and lifecycle: `Fluxo/App.xaml.cs`
- Main UI shell: `Fluxo/Views/Shell/Main/MainWindow.xaml(.cs)`
- Presentation registration: `Fluxo/Extensions/ServiceCollectionExtensions.cs`
- Persistence registration: `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`
- Database model/path: `Fluxo.Data/Context/FluxoDbContext.cs`, `FluxoDbContextFactory.cs`
- Data operation boundary: `Fluxo.Data/Operations/DataOperationRunner.cs`
- Runtime migrations: `Fluxo/App.xaml.cs`, `Fluxo/Migrations/`
- Installer process: `Fluxo.Installer/Program.cs`, `Fluxo.Installer/BootstrapperEntry.cs`
- Packaging: `Fluxo.Installer.Msi/Package.wxs`, `Fluxo.Installer.Bundle/Bundle.wxs`
- CI/release workflow: `.github/workflows/build-on-final-commit.yml`
