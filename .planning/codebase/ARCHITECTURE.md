# Architecture

**Analysis Date:** 2026-04-14

## Pattern Overview

**Overall:** MVVM (Model-View-ViewModel) + Clean Architecture with Domain-Driven Design principles

**Key Characteristics:**
- Clear separation of concerns with 4 distinct project layers (Core, Data, Services, UI)
- Dependency Injection via Microsoft.Extensions.DependencyInjection for loose coupling
- Repository and Unit of Work patterns for data access abstraction
- MVVM toolkit for reactive property bindings and commands
- Generic repository pattern with both read and write specializations
- ViewModel transformation layer that adapts domain entities for UI consumption

## Layers

**Presentation Layer (Fluxo):**
- Purpose: WPF UI rendering, user interaction, and MVVM view models
- Location: `Fluxo/`
- Contains: 
  - Views (XAML + code-behind) in `Fluxo/Views/`
  - ViewModels in `Fluxo/ViewModels/`
  - Converters in `Fluxo/Converters/`
  - App configuration and bootstrapping
- Depends on: Core (entities, enums), Data (repositories), Services (business logic)
- Used by: End users via WPF application

**Application/Services Layer (Fluxo.Services):**
- Purpose: Cross-cutting business logic, cleanup operations, and persistence services
- Location: `Fluxo.Services/`
- Contains:
  - `Fluxo.Services/Persistence/ExpenseCleanupService.cs` - Handles expense data cleanup
- Depends on: Core (interfaces, entities), Data (repositories)
- Used by: Presentation layer ViewModels

**Data Access Layer (Fluxo.Data):**
- Purpose: Entity Framework Core context, repositories, and database interactions
- Location: `Fluxo.Data/`
- Contains:
  - `DbContext` (FluxoDbContext) - EF Core database context with all DbSets
  - Repository implementations - Generic base `Repository<T>` and specialized repositories for each entity
  - UnitOfWork (`UnitOfWork.cs`) - Aggregates all repositories with single SaveChangesAsync
  - EF Core migrations in `Fluxo.Data/Migrations/`
- Depends on: Core (entities, interfaces)
- Used by: Services and Presentation layers

**Domain Layer (Fluxo.Core):**
- Purpose: Domain entities, enums, interfaces, and business rules
- Location: `Fluxo.Core/`
- Contains:
  - Entities: `Expense.cs`, `ExpenseLog.cs`, `IncomeLog.cs`, `SavingGoal.cs`, `SpendingSource.cs`, `UserSettings.cs`, `ExpenseTag.cs`
  - Enums: `ExpenseKind`, `ExpenseCategory`, `SpendingSourceType`, `NotificationSeverity`
  - Interfaces: `IUnitOfWork`, `IRepository<T>`, and specialized repository interfaces
  - Constants: User setting names
- Depends on: Nothing (no outbound dependencies)
- Used by: All other layers

## Data Flow

**User Action → View → ViewModel → Repository → Database:**

1. User interacts with WPF View (e.g., clicks a button to add expense)
2. View triggers ViewModel command or property binding change
3. ViewModel calls repository method via injected `IUnitOfWork` or `IViewModelReadUnitOfWork`
4. Repository queries or modifies entity via `DbSet` on `FluxoDbContext`
5. Changes persisted via `unitOfWork.SaveChangesAsync()` → `dbContext.SaveChangesAsync()`
6. ViewModel receives mapped ViewModel entities and updates observable properties
7. View automatically updates via WPF data binding

**State Management:**
- Presentation state: Managed by ViewModel observable properties (CommunityToolkit.Mvvm)
- Domain state: Persisted in SQLite via Entity Framework Core
- Cross-ViewModel communication: Weak event messaging via CommunityToolkit.Mvvm.Messaging (e.g., `ExpenseDetailUpdatedMessage`)
- ViewModel lifecycle: Singletons (`MainVM`, `DayOfWeekVM`, `MainWindow`) for main window; transient for dialogs/popups

## Key Abstractions

**IUnitOfWork:**
- Purpose: Transaction boundary for data changes across multiple repositories
- Implementation: `Fluxo.Data.UnitOfWork`
- Properties: All repository interfaces (Expenses, ExpenseLogs, IncomeLogs, ExpenseTags, SavingGoals, SpendingSources, UserSettings)
- Pattern: Factory pattern via `Func<IUnitOfWork>` injected for creating fresh instances per operation

**IRepository<T> / IReadRepository<T> / IWriteRepository<T>:**
- Purpose: Abstract data access operations
- Implementation: Generic base `Repository<T>` + specialized repositories per entity
- Read operations: `GetAllAsync()`, `GetByIdAsync()`
- Write operations: `AddAsync()`, `Update()`, `Remove()`, `SaveChangesAsync()`
- Pattern: Generic repository with DDD-like repositories (ExpenseRepository, SpendingSourceRepository, etc.)

**ViewModel Repository Wrappers:**
- Purpose: Transform domain entities to ViewModels with AutoMapper
- Examples: `ExpenseViewModelReadRepository<ExpenseVM>`, `ViewModelWriteRepository<Expense, ExpenseVM>`
- Location: `Fluxo/ViewModels/Persistence/`
- Pattern: Decorator pattern wrapping domain repositories

**Entity Mapping:**
- Purpose: Bidirectional transformation between domain entities and ViewModels
- Implementation: `AutoMapper` via `EntityViewModelProfile` in `Fluxo/Mappings/EntityViewModelProfile.cs`
- Maps: Expense ↔ ExpenseVM, SpendingSource ↔ SpendingSourceVM, etc.

## Entry Points

**App.OnStartup:**
- Location: `Fluxo/App.xaml.cs`
- Triggers: Application start (WPF startup event)
- Responsibilities:
  1. Configure ServiceCollection with all dependencies (Data, Presentation, UI)
  2. Check if first run (query UserSettings via UnitOfWork)
  3. Show StartupWizardPopup if first run, else show StartupLoaderPopup
  4. Initialize MainVM
  5. Show MainWindow

**MainVM.Initialize:**
- Location: `Fluxo/ViewModels/Shell/MainVM.cs`
- Triggers: Called after startup wizard or on app startup
- Responsibilities:
  1. Load all data from repositories (expenses, logs, tags, spending sources, saving goals)
  2. Populate observable collections for UI binding
  3. Calculate derived values (spent amounts, percentages, notifications)
  4. Wire up event handlers and messaging

**Popup ViewModels:**
- Examples: `StartupWizardVM`, `ExpenseDetailVM`, `QuickAddVM`
- Pattern: Created as transient instances with fresh UnitOfWork factories
- Responsibilities: Handle form submission, validation, and persistence for dialogs

## Error Handling

**Strategy:** Try-catch at entry points; fail-fast with user-facing message boxes

**Patterns:**
- App.OnStartup wraps initialization in try-catch, shows MessageBox on error
- Repository pattern includes null-safe checks (e.g., `GetByIdAsync()` returns `T?`)
- Entity Framework Core constraints (required properties, foreign key restrictions) enforced at DB level
- Validation via FluentValidation in Services layer

## Cross-Cutting Concerns

**Logging:** Serilog configured for file-based logging; not heavily instrumented in codebase

**Validation:** FluentValidation package available; applied via Services layer

**Authentication:** Not applicable (local desktop app with single user)

**Data Persistence:** Entity Framework Core with SQLite; migrations tracked in version control

**UI Refresh:** CommunityToolkit.Mvvm messaging for cross-ViewModel communication (e.g., when expense detail updates, main view notified)

---

*Architecture analysis: 2026-04-14*
