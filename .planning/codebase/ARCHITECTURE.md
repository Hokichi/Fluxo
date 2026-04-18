# Architecture

**Analysis Date:** 2026-04-18

## Pattern Overview

**Overall:** Layered MVVM (Model-View-ViewModel) WPF desktop application backed by Repository + Unit-of-Work persistence and a Service layer that mediates between persistence and presentation.

**Key Characteristics:**
- Strict layering across four .NET projects: `Fluxo.Core` (domain contracts), `Fluxo.Data` (EF Core persistence), `Fluxo.Services` (application services), `Fluxo` (WPF presentation/composition root)
- CommunityToolkit.Mvvm-driven ViewModels (`ObservableObject`, `ObservableRecipient`, `[ObservableProperty]`, `[RelayCommand]`)
- AutoMapper used in two stages: `Entity ⇄ DTO` (in `Fluxo.Services`) and `DTO ⇄ ViewModel` (in `Fluxo`)
- Composition handled by `Microsoft.Extensions.DependencyInjection` registered in `Fluxo/Extensions/ServiceCollectionExtensions.cs` and `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`
- Cross-VM communication via `WeakReferenceMessenger` from CommunityToolkit.Mvvm
- Local SQLite database created in the app base directory (`fluxo.db`); migrations live in the WPF project (`Fluxo/Migrations/`)
- Single-window WPF shell with custom popup framework (`Fluxo/Resources/CustomControls/BasePopup.cs`, `IPopupHost.cs`, `FluxoMessageBox.cs`)

## Layers

**Fluxo.Core (Domain / Contracts):**
- Purpose: Defines entities, DTOs, enums, filter objects, and abstractions (interfaces) for repositories, services, and unit-of-work
- Location: `Fluxo.Core/`
- Contains: POCO entities (`Fluxo.Core/Entities/Expense.cs`), DTOs (`Fluxo.Core/DTO/ExpenseDto.cs`), enums (`Fluxo.Core/Enums/ExpenseCategory.cs`), filter classes (`Fluxo.Core/Filters/ExpenseFilter.cs`), repository contracts (`Fluxo.Core/Interfaces/Repositories/IRepository.cs`), service contracts (`Fluxo.Core/Interfaces/Services/IExpenseService.cs`), and `Fluxo.Core/Interfaces/IUnitOfWork.cs`
- Depends on: Nothing (no project references)
- Used by: `Fluxo.Data`, `Fluxo.Services`, `Fluxo`

**Fluxo.Data (Persistence):**
- Purpose: Implements EF Core 10 (SQLite) persistence — `DbContext`, configurations, repositories, and `UnitOfWork`
- Location: `Fluxo.Data/`
- Contains: `Fluxo.Data/Context/FluxoDbContext.cs`, `Fluxo.Data/Context/FluxoDbContextFactory.cs`, generic base `Fluxo.Data/Repositories/Repository.cs`, entity-specific repositories (`Fluxo.Data/Repositories/ExpenseRepository.cs`, etc.), `Fluxo.Data/UnitOfWork.cs`, DI wiring `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`
- Depends on: `Fluxo.Core`
- Used by: `Fluxo.Services`, `Fluxo`

**Fluxo.Services (Application Services):**
- Purpose: Orchestrates domain operations across repositories, applies AutoMapper Entity↔DTO transforms, enforces business rules (e.g., balance adjustments on expense add/remove)
- Location: `Fluxo.Services/`
- Contains: `Fluxo.Services/Persistence/ExpenseService.cs`, `ExpenseLogService.cs`, `SpendingSourceService.cs`, `TagService.cs`, AutoMapper profile `Fluxo.Services/Mappings/EntityDtoProfile.cs`
- Depends on: `Fluxo.Core`, `Fluxo.Data`
- Used by: `Fluxo`

**Fluxo (Presentation / Composition Root):**
- Purpose: WPF UI, ViewModels, dialog/history services, AutoMapper DTO→VM profile, app bootstrap, EF Core migrations assembly
- Location: `Fluxo/`
- Contains: `Fluxo/App.xaml.cs` (composition + startup), `Fluxo/Extensions/ServiceCollectionExtensions.cs` (DI for presentation), Views (`Fluxo/Views/`), ViewModels (`Fluxo/ViewModels/`), UI services (`Fluxo/Services/Dialogs/`, `Fluxo/Services/History/`), value converters (`Fluxo/Converters/`), DTO↔VM mapping (`Fluxo/Mappings/DtoViewModelProfile.cs`), EF migrations (`Fluxo/Migrations/`)
- Depends on: `Fluxo.Core`, `Fluxo.Data`, `Fluxo.Services`
- Used by: `Fluxo.Tests`

**Fluxo.Tests (Tests):**
- Purpose: xUnit unit tests for ViewModels, view-side helpers, and dialog service
- Location: `Fluxo.Tests/`
- Depends on: `Fluxo` (which transitively pulls Core/Data/Services)

## Data Flow

**Read flow (e.g., loading dashboard data in `Fluxo/ViewModels/Shell/MainVM.cs`):**

1. `MainVM.Initialize()` calls service methods (e.g., `_expenseService.GetAllAsync()`)
2. Service (`Fluxo.Services/Persistence/ExpenseService.cs`) delegates to `IUnitOfWork.Expenses.GetAllAsync()`
3. Repository (`Fluxo.Data/Repositories/ExpenseRepository.cs`) issues an EF Core query (with `AsNoTracking` / `AsNoTrackingWithIdentityResolution` and `Include` for navigations)
4. Service maps `Entity → DTO` via `IMapper` (`EntityDtoProfile`)
5. ViewModel maps `DTO → ViewModel` via `IMapper` (`DtoViewModelProfile`) and binds to view
6. WPF View binds to VM observable properties

**Write flow (e.g., adding an expense in `Fluxo.Services/Persistence/ExpenseService.cs`):**

1. ViewModel constructs a `*Dto` and calls the service
2. Service validates referenced aggregates (e.g., `SpendingSource`), constructs entities, calls `IUnitOfWork.<Repository>.AddAsync(...)`, mutates related aggregates, then `await unitOfWork.SaveChangesAsync()`
3. ViewModel calls `MainVM.ReloadCurrentDataAsync()` (or sends a message) to refresh the UI

**State Management:**
- UI state lives in singleton VMs registered in `Fluxo/Extensions/ServiceCollectionExtensions.cs` (`MainVM`, `DaySpinnerVM`, `BudgetAllocationPanelVM`, `NotificationPanelVM`, `SavingGoalsPanelVM`, `MainViewModeToggleVM`, `DayOfWeekVM`)
- Per-record / per-popup VMs are registered transient (e.g., `ExpenseVM`, `QuickAddVM`, `AddSpendingSourceVM`, `SettingsVM`, `StartupWizardVM`)
- Cross-VM events use `WeakReferenceMessenger.Default` with strongly-typed message classes in `Fluxo/ViewModels/Messages/`
- Undo/redo is local to the shell window via `Fluxo/Services/History/LogMemoryManager.cs`, which records `ILogMemoryAction` instances onto undo/redo stacks and replays them through `IUnitOfWork`

## Key Abstractions

**`IUnitOfWork` (`Fluxo.Core/Interfaces/IUnitOfWork.cs`):**
- Purpose: Aggregates all repository accessors and exposes `SaveChangesAsync` so a single `DbContext` is shared across operations within a logical transaction
- Implementation: `Fluxo.Data/UnitOfWork.cs` (sealed, primary constructor)
- Pattern: Composition over inheritance — repositories are injected, no service-locator inside

**`IRepository<T>` and `Repository<T>` (`Fluxo.Core/Interfaces/Repositories/IRepository.cs`, `Fluxo.Data/Repositories/Repository.cs`):**
- Purpose: Generic CRUD base with safe attach/update semantics for EF Core change tracking (handles already-tracked entities, eagerly loads reference navigations recursively)
- Pattern: Open-generic base with sealed entity-specific subclasses (e.g., `ExpenseRepository : Repository<Expense>, IExpenseRepository`); also exposes `IReadRepository<T>`, `IWriteRepository<T>` segregations

**Application Services (`Fluxo.Services/Persistence/*Service.cs`):**
- Purpose: Coordinate multi-aggregate operations (e.g., adding an expense also creates an `ExpenseLog` and adjusts `SpendingSource.Balance/SpentAmount`)
- Pattern: Constructor-injected `IUnitOfWork` + `IMapper`; methods accept/return DTOs, never entities

**ViewModels (`Fluxo/ViewModels/`):**
- Purpose: Bindable surface for views; all extend `ObservableObject`/`ObservableRecipient` from CommunityToolkit.Mvvm
- Pattern: `partial` classes using source generators (`[ObservableProperty]`, `[RelayCommand]`); large VMs split across partials (e.g., `MainVM.cs` + `MainVM.ExpenseDetailMessenger.cs`, `SavingGoalVM.cs` + `SavingGoalVM.Progress.cs`)
- Hierarchy: `Entities/` (record-shaped VMs), `Shell/` and `Shell/Main/` (long-lived shell VMs), `Popups/` (per-dialog VMs), `Controls/` (control-scoped VMs), `Notifications/`, `Helpers/`, `Messages/`

**`IDialogService` (`Fluxo/Services/Dialogs/IDialogService.cs`, impl `DialogService.cs`):**
- Purpose: Centralized factory for opening modal popups so VMs/Views never `new` popup windows directly
- Pattern: Service-locator-free; resolves popups through the shared `IServiceProvider` for DI-managed popups, constructs simple popups inline

**Mapping Profiles:**
- `Fluxo.Services/Mappings/EntityDtoProfile.cs` — `Entity ⇄ DTO`, with explicit ignores for computed `SpendingSource.MoneyIn/MoneyOut` and DTO→Entity `Id`
- `Fluxo/Mappings/DtoViewModelProfile.cs` — `DTO ⇄ ViewModel`

**Messaging (`Fluxo/ViewModels/Messages/`):**
- `WeakReferenceMessenger.Default` registered as singleton `IMessenger` in `Fluxo/Extensions/ServiceCollectionExtensions.cs`
- Examples: `DashboardDataInvalidatedMessage`, `ExpenseDetailUpdatedMessage`, `UsernameChangedMessage`, `LogMemoryMessages.cs`, `ViewModeChangeMessage`

## Entry Points

**Application bootstrap:**
- Location: `Fluxo/App.xaml.cs`
- Triggers: WPF `Application` startup (project `OutputType=WinExe`, `Fluxo/Fluxo.csproj`)
- Responsibilities: Builds `ServiceCollection` via `AddFluxoData()` (`Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`) + `AddFluxoPresentation()` + `AddUIData()` (`Fluxo/Extensions/ServiceCollectionExtensions.cs`); resolves `MainVM` and `IUnitOfWork`; on `OnStartup`, ensures `IsFirstRun` user setting exists, optionally runs `StartupWizardPopup`, otherwise shows `StartupLoaderPopup` while `MainVM.Initialize()` runs, then shows `MainWindow`

**Main shell:**
- Location: `Fluxo/Views/Shell/Main/MainWindow.xaml(.cs)`
- Triggers: Resolved as singleton from DI after startup
- Responsibilities: Hosts dashboard panels (`BudgetAllocationPanelHost`, `NotificationPanelHost`, `SavingGoalsPanelHost`, `DaySpinnerControlHost`, `ViewModeToggleControlHost`), wires `LogMemoryManager` for undo/redo, implements `IPopupHost`, handles custom window chrome and keyboard shortcuts (see `Fluxo/Views/Shell/Main/MainWindowShortcutMatcher.cs`)

**Database/migrations entry points:**
- Runtime DbContext factory: `Fluxo.Data/Context/FluxoDbContextFactory.cs` (writes `fluxo.db` next to `AppContext.BaseDirectory`, `MigrationsAssembly("Fluxo")`)
- Design-time factory for `dotnet ef`: `Fluxo/Migrations/FluxoDesignTimeDbContextFactory.cs`

**Wizard / first-run:**
- Location: `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml(.cs)` with pages under `Fluxo/Views/Shell/Wizard/Pages/` and steps under `Fluxo/Views/Shell/Wizard/Pages/Steps/`
- Triggered when `UserSettings.IsFirstRun` is missing or `true` (see `App.EnsureFirstRunSettingAsync`)

## Error Handling

**Strategy:** Defensive validation in services with thrown `InvalidOperationException` for missing aggregates; UI-level catches in `App.OnStartup` route to `IDialogService.ShowError` (or `FluxoMessageBox.Show` fallback).

**Patterns:**
- Service-layer guards: e.g., `ExpenseService.AddAsync` validates `SpendingSource` exists before staging entities (`Fluxo.Services/Persistence/ExpenseService.cs`)
- Soft-delete semantics on `ExpenseLog.IsForDeletion` so deletes can be undone via `LogMemoryManager`
- Repository update/remove logic in `Fluxo.Data/Repositories/Repository.cs` defensively reconciles already-tracked entities to avoid duplicate-tracking exceptions
- `Fluxo.Core/Exceptions/` folder is reserved (currently empty) for future custom exception types

## Cross-Cutting Concerns

**Logging:** `Serilog` + `Serilog.Sinks.File` referenced in `Fluxo/Fluxo.csproj`; no central bootstrap configuration found in code (logger is not yet wired into DI as of this snapshot).

**Validation:** `FluentValidation` 12.x referenced in `Fluxo.Services/Fluxo.Services.csproj` (validators not yet present in repo).

**Authentication:** Not applicable — single-user local desktop app; "user" is just a display-name preference stored in `UserSettings` (`Fluxo.Core/Constants/UserSettingNames.cs`).

**Notifications (toast):** `Microsoft.Toolkit.Uwp.Notifications` is referenced in `Fluxo.Services/Fluxo.Services.csproj`; in-app notifications are surfaced via `Fluxo/ViewModels/Notifications/NotificationItemVM.cs` and assembled in `MainVM.EvaluateSystemNotifications()`.

**Theming/styling:** Centralized resource dictionaries in `Fluxo/App.xaml` (Theme, Icons, Fonts/ContainerStyles, Styles/*).

**Background work:** None — all I/O is async-on-UI-thread via `await`.

---

*Architecture analysis: 2026-04-18*
