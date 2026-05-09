# Architecture

**Analysis Date:** 2026-05-09

## System Shape

Fluxo is a Windows-only, layered WPF desktop application organized as a modular monolith:

- `Fluxo` is the executable application, composition root, and primary UI shell.
- `Fluxo.Core` holds domain entities, DTOs, enums, filters, exceptions, and service/repository/operation contracts.
- `Fluxo.Data` implements EF Core SQLite persistence, repositories, unit of work, and scoped data operation execution.
- `Fluxo.Services` implements application services, mapping profiles, notification logic, logging, and persistence use cases.
- `Fluxo.Resources` packages reusable WPF resources, custom controls, converters, styles, fonts, icons, and UI messages.
- `Fluxo.Installer` is a managed WiX Burn bootstrapper UI.
- `Fluxo.Installer.Msi` authors the MSI package for app files.
- `Fluxo.Installer.Bundle` authors the outer bootstrapper bundle.
- `Fluxo.Tests` contains xUnit coverage for infrastructure, services, view models, views/layout helpers, and installer behavior.

Project references flow mostly inward:

```text
Fluxo
  -> Fluxo.Core
  -> Fluxo.Data
  -> Fluxo.Services
  -> Fluxo.Resources

Fluxo.Services -> Fluxo.Core, Fluxo.Data
Fluxo.Data     -> Fluxo.Core
Fluxo.Resources -> Fluxo.Core, Fluxo.Services
Fluxo.Installer -> Fluxo.Resources
Fluxo.Tests -> Fluxo, Fluxo.Installer
```

## Layer Responsibilities

### Domain and Contracts

`Fluxo.Core` defines the stable vocabulary shared across the app:

- Entities: `Expense`, `ExpenseLog`, `IncomeLog`, `ExpenseTag`, `SavingGoal`, `SpendingSource`, `Notification`, `UserSettings`.
- DTOs for service/UI transfer.
- Enums and filters for spending source type, expense category/kind, dashboard mode, close behavior, notification severity, settings batch operations, and query filters.
- Interfaces for repositories, services, unit of work, data operations, popup host abstractions, logging, and history actions.
- `DataOperationException` for wrapping persistence boundary failures.

### Persistence

`Fluxo.Data` owns SQLite access through EF Core:

- `FluxoDbContext` maps all domain entities, decimal money columns as `NUMERIC`, interest rate as `REAL`, required string properties, restrict-delete relationships, and auto-includes reference navigations.
- `FluxoDbContextFactory` builds a SQLite connection to `fluxo.db` under `AppContext.BaseDirectory` and sets the migrations assembly to `Fluxo`.
- Repository implementations live behind `Fluxo.Core.Interfaces.Repositories`.
- `UnitOfWork` aggregates repositories and commits through `SaveChangesAsync`.
- `DataOperationScopeFactory` creates async DI scopes.
- `DataOperationRunner` is the preferred boundary for UI/service operations: it creates a scope, executes the callback, logs failures through `ILogService`, and wraps unexpected exceptions in `DataOperationException`.

The main exception to the runner boundary is `AppDataService`, which is scoped and uses `IUnitOfWork` directly for startup/setup batch persistence.

### Application Services

`Fluxo.Services` contains DTO-oriented use-case services:

- `Persistence/*Service.cs` handles expenses, logs, spending sources, tags, analytics, and app setup data.
- Services generally use `IDataOperationRunner` and AutoMapper to convert entities to DTOs.
- `Mappings/EntityDtoProfile.cs` defines entity-to-DTO mapping.
- `Logging/FluxoLogService.cs` adapts app logging contracts to the Serilog-backed logging manager.
- Notification services group notifications, apply notification actions, and build startup tray summaries.

### UI and MVVM

`Fluxo` is a WPF MVVM shell using CommunityToolkit.Mvvm:

- View models derive from `ObservableObject` or `ObservableRecipient`.
- `[ObservableProperty]` and `[RelayCommand]` drive generated properties and commands.
- `WeakReferenceMessenger.Default` and registered `IMessenger` are used for cross-view-model coordination.
- `DtoViewModelProfile` maps DTOs into view-model types.
- Views are grouped by workflow under `Views/Shell`, `Views/Popups`, and `Views/Behaviors`.
- Main dashboard state is composed by `MainVM`, which owns panels for budget allocation, spent allowance, notifications, saving goals, day spinner, and view-mode toggle.
- Popups and settings tabs have dedicated view models; settings changes communicate through messages such as pending-change, apply, revert, load, and data-changed messages.
- Quick setup has a dedicated wizard shell and step view models for identity, spending sources, fixed expenses, saving goals, budget allocation, notification preferences, and summary.

`Fluxo.Resources` supplies reusable visual infrastructure rather than application startup logic:

- `Resources/*.xaml` dictionaries for theme, fonts, icons, converters, and styles.
- `CustomControls` for popups, money input, step navigation, swipe reveal, balloons, fading scroll, and shared popup handoff state.
- `Components` for reusable XAML components such as charts, date selector, income source, expenses list, message box popup, and wave/background visuals.
- `Resources/Messages` contains CommunityToolkit message payload types shared by shell, settings, wizard, and history workflows.

## Runtime Data Flow

Typical dashboard/service flow:

1. A view binds to a view model command or property.
2. The view model calls an application service or `IDataOperationRunner`.
3. `DataOperationRunner` creates an async DI scope.
4. The scope resolves `IUnitOfWork`, repositories, and `FluxoDbContext`.
5. Repositories query or mutate EF entities.
6. `UnitOfWork.SaveChangesAsync` commits changes.
7. Services map entities to DTOs; UI mapping maps DTOs to view models where needed.
8. View models send messenger notifications to refresh related panels or record history actions.

Data invalidation is message-driven in several workflows. Examples include dashboard refresh, expense detail updates, date-range changes, view-mode changes, startup wizard draft changes, and settings apply/revert/load cycles.

## Startup and Lifetime

`Fluxo/App.xaml.cs` is the app entry point and lifetime coordinator:

- Registers global dispatcher, app-domain, and unobserved-task exception handlers.
- Builds a `ServiceCollection` directly through `AddFluxoData()`, `AddFluxoPresentation()`, and `AddUIData()`.
- Enforces single-instance startup through `SingleInstanceCoordinator` and `SingleInstanceStartupPolicy`.
- Sets `ShutdownMode` to explicit shutdown during startup and switches to main-window close after the shell is ready.
- Supports tray launch with `--startup-tray` and restart launch with `--startup--tray`.
- Initializes logging after resolving the configured username when possible.
- Shows `StartupLoaderPopup` while migrating the database, ensuring the first-run setting, cleaning terminated expense logs, syncing Windows startup registration, and loading `MainVM` startup stages.
- Shows `QuickSetupWizard` on first run; otherwise resolves and shows `MainWindow` or hides it to tray.
- Maintains a WinForms `NotifyIcon`, tray menu popup, startup notification popup, close-to-tray behavior, and restart/exit actions.
- Disposes tray resources and the single-instance coordinator on exit, then flushes logging.

Database startup is explicit:

- `MigrateDatabaseAsync` runs through `IDataOperationRunner`.
- If a database has no migration history and no application tables, it recreates and seeds migration history.
- If an older database lacks migration history but has recognizable schema shape, startup infers the latest applied migration and seeds `__EFMigrationsHistory`.
- Normal EF migrations then run via `Database.MigrateAsync`.

## Persistence Boundaries

Primary persistence boundary:

- UI/ViewModel -> service interface or `IDataOperationRunner`.
- Service -> `IDataOperationRunner`.
- Runner -> scoped `IUnitOfWork`.
- Unit of work -> repositories -> `FluxoDbContext`.

Important boundary details:

- `FluxoDbContext` is scoped.
- Repositories and `IUnitOfWork` are scoped.
- `IDataOperationScopeFactory` and `IDataOperationRunner` are singleton, but they create scopes per operation.
- Main dashboard view models are singleton; most popup, wizard, entity, and settings view models are transient.
- Some setup-oriented logic uses scoped services directly where a short-lived wizard scope is created.

## Installer Architecture

Installation is split into a managed bootstrapper, MSI package, and bundle:

- `Fluxo.Installer` is a WPF managed bootstrapper application using WiX BootstrapperApplication API.
- `Program.Main` protects the installer with a global mutex and runs `ManagedBootstrapperApplication`.
- `InstallerBootstrapperApplication` wires Burn detect/plan/apply events, chooses interactive vs headless behavior, detects related bundles, handles up-to-date decisions, suppresses related bundle execution during upgrade cleanup, and dispatches event results to the WPF UI thread.
- `InstallerViewModel` owns install/repair/uninstall UI state, prerequisite checks, checklist transitions, launch action selection, rollback/cleanup behavior, running app termination confirmation, repairer copy, and deferred cleanup script creation.
- `Fluxo.Installer.Msi` builds the MSI package and invokes the main `Fluxo` app build before MSI build.
- `Fluxo.Installer.Bundle` builds the outer bundle, embeds the managed bootstrapper payloads, and chains the MSI with `INSTALLFOLDER` passed from the bundle variable.
- The bundle writes to `C:\Program Files\fluxo` by default and produces `fluxo-$(FluxoInstallerVersion)-Installer`.

Installer tests cover up-to-date decisions, operation-mode detection, flow state, launch commands, MSI authoring expectations, runtime detection, and installed-version registry reading.

## Output and Packaging Behavior

The main app targets `net10.0-windows`, `WinExe`, `win-x64`, and WPF:

- `Fluxo.csproj` organizes build/publish output by moving first-party DLLs to `libs/` and vendor DLLs to `vendor/`.
- Build output creates a hard link for the root executable when possible.
- PDB files are deleted after organized build output.
- Installer projects consume the `win-x64` output shape.

## Cross-Cutting Concerns

- Mapping: AutoMapper profiles bridge entity/DTO and DTO/view-model layers.
- Messaging: CommunityToolkit messenger coordinates decoupled UI refreshes and workflow events.
- Logging: Serilog-backed `FluxoLogManager` is initialized during app startup and used through `ILogService` or direct startup exception handlers.
- Single instance: app startup coordinates primary/secondary activation and restores the existing window from tray.
- Notifications: notification grouping/action services handle app notifications and startup tray summaries.
- History: log memory actions are recorded and replayed through messenger-backed history infrastructure.
- Startup registration: Windows run-at-startup behavior is synced from `UserSettings`.

## Architectural Notes for Future Work

- Keep domain contracts in `Fluxo.Core`; avoid adding UI or EF-specific behavior there.
- Prefer `IDataOperationRunner` for persistence work from singleton view models and services.
- Keep migrations in `Fluxo/Migrations` because the runtime configuration sets `MigrationsAssembly("Fluxo")`.
- Treat `Fluxo.Resources` as shared WPF infrastructure; adding application-specific orchestration there increases coupling because it already references services.
- Installer behavior spans WiX authoring and managed WPF state; changes usually need both XML and `InstallerViewModel`/bootstrapper tests.

---

*Architecture analysis refreshed: 2026-05-09*
