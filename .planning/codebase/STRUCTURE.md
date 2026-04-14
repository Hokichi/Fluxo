# Codebase Structure

**Analysis Date:** 2026-04-14

## Directory Layout

```
Fluxo/ (Solution Root)
├── Fluxo/                          # Main WPF presentation project
│   ├── App.xaml                    # Application root; dependency injection setup
│   ├── App.xaml.cs                 # Startup logic, first-run wizard
│   ├── Fluxo.csproj                # Project file with WPF config
│   ├── Converters/                 # XAML value converters
│   │   ├── BoolToVisibilityConverter.cs
│   │   ├── DateTimeToRelativeDateConverter.cs
│   │   ├── GoalProgressToBrushConverter.cs
│   │   ├── NumberWithCommasConverter.cs
│   │   └── [8 more converters]
│   ├── Extensions/
│   │   └── ServiceCollectionExtensions.cs  # DI configuration for presentation layer
│   ├── Mappings/
│   │   └── EntityViewModelProfile.cs       # AutoMapper configuration
│   ├── Migrations/                         # EF Core migrations (local for testing)
│   ├── Resources/
│   │   ├── Fonts/                  # Custom font files (SFTSchriftedRoundTRIAL)
│   │   ├── CustomControls/         # XAML custom control implementations
│   │   │   ├── BalloonBorder.xaml
│   │   │   ├── BalloonButton.cs
│   │   │   ├── SwipeRevealContainer.cs
│   │   │   └── FadingScrollViewer.cs
│   │   ├── Styles/                 # XAML style resources
│   │   │   └── ButtonStyles.xaml
│   │   ├── Theme.xaml              # Global theme and color definitions
│   │   ├── Icons.xaml              # Icon definitions
│   │   └── icon.ico                # Application icon
│   ├── Services/                   # Presentation-layer services
│   │   └── History/                # Undo/redo command history
│   ├── ViewModels/                 # MVVM ViewModels
│   │   ├── Controls/               # Non-entity ViewModels (e.g., DayOfWeekVM)
│   │   ├── Entities/               # Entity ViewModels
│   │   │   ├── ExpenseVM.cs
│   │   │   ├── ExpenseLogVM.cs
│   │   │   ├── IncomeLogVM.cs
│   │   │   ├── SpendingSourceVM.cs
│   │   │   ├── SavingGoalVM.cs
│   │   │   └── [more entity VMs]
│   │   ├── Messages/               # Messaging payloads for cross-ViewModel events
│   │   │   ├── ExpenseDetailUpdatedMessage.cs
│   │   │   ├── LogMemoryMessages.cs
│   │   │   └── UsernameChangedMessage.cs
│   │   ├── Notifications/          # Notification display ViewModels
│   │   │   └── NotificationItemVM.cs
│   │   ├── Persistence/            # ViewModel-specific repository wrappers
│   │   │   ├── EntityViewModelReadUnitOfWork.cs
│   │   │   ├── EntityViewModelWriteUnitOfWork.cs
│   │   │   ├── ExpenseViewModelReadRepository.cs
│   │   │   ├── ViewModelReadRepository.cs
│   │   │   └── ViewModelWriteRepository.cs
│   │   └── Shell/                  # Main window and app shell ViewModels
│   │       └── MainVM.cs           # Main application ViewModel
│   └── Views/                      # XAML UI views and code-behind
│       ├── Components/             # Reusable UI components
│       │   ├── DateSelector.xaml
│       │   ├── ExpensesList.xaml
│       │   └── IncomeSource.xaml
│       ├── Popups/                 # Modal dialog windows
│       │   ├── AddFixedExpensePopup.xaml
│       │   ├── AddSavingGoalPopup.xaml
│       │   ├── ExpenseDetailPopup.xaml
│       │   ├── QuickAddPopup.xaml
│       │   ├── SettingsPopup.xaml
│       │   ├── StartupWizardPopup.xaml
│       │   └── [11 more popups]
│       └── Shell/                  # Main application windows
│           ├── MainWindow.xaml
│           └── MainWindow.xaml.cs
├── Fluxo.Core/                     # Domain entity and interface layer
│   ├── Constants/
│   │   └── UserSettingNames.cs     # Constant strings for user setting keys
│   ├── Entities/                   # Domain models
│   │   ├── Expense.cs
│   │   ├── ExpenseLog.cs           # Log entry for each expense transaction
│   │   ├── IncomeLog.cs            # Log entry for income
│   │   ├── SavingGoal.cs
│   │   ├── SpendingSource.cs       # Account/wallet
│   │   ├── UserSettings.cs         # Key-value app settings
│   │   └── ExpenseTag.cs           # Tag/category for expenses
│   ├── Enums/
│   │   ├── ExpenseKind.cs          # Fixed, Variable, Invest, etc.
│   │   ├── ExpenseCategory.cs      # Needs, Wants, Invest
│   │   ├── SpendingSourceType.cs   # Account type
│   │   └── NotificationSeverity.cs
│   ├── Interfaces/
│   │   ├── IExpenseCleanupService.cs
│   │   ├── IUnitOfWork.cs          # Main transaction boundary
│   │   ├── IViewModelReadUnitOfWork.cs
│   │   ├── IViewModelWriteUnitOfWork.cs
│   │   └── Repositories/           # Repository interface contracts
│   │       ├── IRepository.cs
│   │       ├── IReadRepository.cs
│   │       ├── IWriteRepository.cs
│   │       ├── IExpenseRepository.cs
│   │       └── [more specific repos]
│   ├── Exceptions/                 # Placeholder for custom exceptions
│   └── Fluxo.Core.csproj
├── Fluxo.Data/                     # Data access and persistence layer
│   ├── Context/
│   │   ├── FluxoDbContext.cs       # EF Core DbContext
│   │   └── FluxoDbContextFactory.cs # Factory for creating db context instances
│   ├── Extensions/
│   │   └── ServiceCollectionExtensions.cs # DI registration for repositories
│   ├── Migrations/                 # EF Core database migrations
│   │   ├── 20260331080056_InitialCreate.cs
│   │   ├── 20260331083448_AddLimitAndSpentAmountToSpendingSource.cs
│   │   └── [3 more migrations]
│   ├── Repositories/               # Repository implementations
│   │   ├── Repository.cs           # Generic base repository
│   │   ├── ExpenseRepository.cs
│   │   ├── ExpenseLogRepository.cs
│   │   ├── IncomeLogRepository.cs
│   │   ├── SpendingSourceRepository.cs
│   │   ├── SavingGoalRepository.cs
│   │   ├── ExpenseTagRepository.cs
│   │   └── UserSettingsRepository.cs
│   ├── UnitOfWork.cs               # Aggregates all repositories
│   ├── Configurations/             # Placeholder for EF configurations
│   └── Fluxo.Data.csproj
├── Fluxo.Services/                 # Business logic and domain services
│   ├── Persistence/
│   │   └── ExpenseCleanupService.cs # Service for cleaning marked-for-deletion expenses
│   ├── Calculations/               # Placeholder for business calculations
│   ├── Extensions/                 # Placeholder for service extensions
│   └── Fluxo.Services.csproj
├── Fluxo.slnx                      # Solution file
├── docs/                           # Documentation
└── .git/                           # Git repository
```

## Directory Purposes

**Fluxo/ (Presentation Layer):**
- Purpose: WPF application UI, view models, and application orchestration
- Contains: XAML views, code-behind, ViewModels, converters, resources
- Key files: `App.xaml.cs` (entry point), `MainVM.cs` (main application state)

**Fluxo/ViewModels/:**
- Purpose: UI state and command logic following MVVM pattern
- Contains:
  - Entity ViewModels wrapping domain entities with observable properties
  - Shell ViewModels for main window coordination
  - Popup ViewModels for dialog/wizard logic
  - Persistence adapters transforming domain repos to ViewModel repos

**Fluxo/Views/:**
- Purpose: XAML UI definitions with minimal code-behind
- Contains:
  - Shell: Main application window
  - Components: Reusable controls (DateSelector, ExpensesList, IncomeSource)
  - Popups: Modal dialogs for forms and wizards

**Fluxo.Core/ (Domain Layer):**
- Purpose: Domain model definitions, enums, and abstractions
- Contains:
  - Entities: Pure data classes (no EF dependencies)
  - Enums: Business domain enumerations
  - Interfaces: Contracts for repositories and services
  - No dependencies on other projects

**Fluxo.Data/ (Data Access Layer):**
- Purpose: Entity Framework Core context, repository implementations, database schema
- Contains:
  - `FluxoDbContext`: EF Core DbContext with all entity mappings
  - Repository implementations: Concrete CRUD operations
  - Migrations: Database schema versioning
  - Dependency: Fluxo.Core only

**Fluxo.Services/ (Application/Business Layer):**
- Purpose: Cross-cutting business logic, domain services
- Contains:
  - `ExpenseCleanupService`: Handles soft-delete logic for expenses
  - Dependencies: Fluxo.Core, Fluxo.Data

## Key File Locations

**Entry Points:**
- `Fluxo/App.xaml.cs`: Application startup, first-run wizard, DI container setup
- `Fluxo/Views/Shell/MainWindow.xaml.cs`: Main application window

**Configuration:**
- `Fluxo/Fluxo.csproj`: WPF project settings, NuGet dependencies
- `Fluxo/Extensions/ServiceCollectionExtensions.cs`: Presentation layer DI configuration
- `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`: Data layer DI configuration
- `Fluxo/Mappings/EntityViewModelProfile.cs`: AutoMapper entity-to-viewmodel mappings
- `Fluxo.Data/Context/FluxoDbContext.cs`: EF Core configuration and schema

**Core Logic:**
- `Fluxo.Core/Entities/`: Domain model classes
- `Fluxo.Core/Interfaces/`: Service and repository contracts
- `Fluxo.Data/Repositories/`: Data access implementations
- `Fluxo/ViewModels/Shell/MainVM.cs`: Main application state management

**Testing:**
- Tests folder location: Not present (no unit tests in codebase)

## Naming Conventions

**Files:**
- ViewModel files: `*VM.cs` or `*ViewModel.cs` (e.g., `ExpenseVM.cs`, `MainVM.cs`)
- Repository files: `*Repository.cs` (e.g., `ExpenseRepository.cs`)
- Dialog/View files: `*Popup.xaml` and `*Popup.xaml.cs` for modals; `*.xaml` + `*.xaml.cs` for standard views
- Converter files: `*Converter.cs` (e.g., `BoolToVisibilityConverter.cs`)
- Message/Event files: `*Message.cs` (e.g., `ExpenseDetailUpdatedMessage.cs`)

**Directories:**
- PascalCase for project names: `Fluxo`, `Fluxo.Core`, `Fluxo.Data`, `Fluxo.Services`
- PascalCase for folders: `ViewModels`, `Repositories`, `Extensions`, `Migrations`
- Functional grouping: `Entities/`, `Interfaces/`, `Converters/`, `Resources/`, `Views/`

**Classes:**
- PascalCase: `MainVM`, `ExpenseVM`, `UserSettings`, `SpendingSource`
- Entity classes: No suffix (e.g., `Expense`, `ExpenseLog`)
- ViewModel classes: `VM` suffix (e.g., `ExpenseVM`, `MainVM`)
- Repository classes: `Repository` suffix (e.g., `ExpenseRepository`)
- Service classes: `Service` suffix (e.g., `ExpenseCleanupService`)

**Methods/Properties:**
- PascalCase for public: `GetAllAsync()`, `SaveChangesAsync()`, `Initialize()`
- camelCase for private: `_serviceProvider`, `_disabledSavingGoalIds`

## Where to Add New Code

**New Feature (Multi-Entity Operation):**
- Primary code:
  - Domain layer: `Fluxo.Core/Entities/` (if new entity), `Fluxo.Core/Enums/` (if new enum)
  - Data layer: `Fluxo.Data/Repositories/` (repository implementation)
  - Data layer: `Fluxo.Data/Migrations/` (schema changes)
  - Services layer: `Fluxo.Services/` (business logic)
  - Presentation: `Fluxo/ViewModels/Entities/` (ViewModel), `Fluxo/Views/` (View)
- Register in DI: `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`, `Fluxo/Extensions/ServiceCollectionExtensions.cs`
- Mapping: `Fluxo/Mappings/EntityViewModelProfile.cs` (add AutoMapper config)

**New Component/Module (UI-Only):**
- Implementation:
  - ViewModel: `Fluxo/ViewModels/[Category]/YourNameVM.cs`
  - View: `Fluxo/Views/[Category]/YourName.xaml` + `.xaml.cs`
  - Register in DI: `Fluxo/Extensions/ServiceCollectionExtensions.cs` if it's a service
- Naming: Use `VM` suffix for ViewModels, group by functional area (Controls, Entities, Shell, Popups)

**Utilities/Converters:**
- Shared helpers: `Fluxo/Converters/` (for XAML converters)
- Extension methods: `[Project]/Extensions/` (e.g., `Fluxo/Extensions/`, `Fluxo.Data/Extensions/`)

**Data Access for Existing Entity:**
- Repository customization: `Fluxo.Data/Repositories/[Entity]Repository.cs`
- Interface: `Fluxo.Core/Interfaces/Repositories/I[Entity]Repository.cs`
- DI registration: `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`

## Special Directories

**Migrations:**
- Purpose: EF Core database schema versioning
- Generated: Yes (auto-generated by `dotnet ef migrations add`)
- Committed: Yes (tracked in version control)
- Location: `Fluxo.Data/Migrations/` (primary) and `Fluxo/Migrations/` (local testing copies)

**Resources:**
- Purpose: XAML styles, themes, fonts, custom controls, application icon
- Generated: No (all static assets)
- Committed: Yes (fonts, styles, icons tracked in git)
- Location: `Fluxo/Resources/`
- Subdirectories:
  - `Fonts/`: Custom TrueType fonts
  - `Styles/`: XAML style definitions
  - `CustomControls/`: Reusable control code-behind and XAML

**bin/ and obj/:**
- Purpose: Compiled binaries and build artifacts
- Generated: Yes (by .NET build process)
- Committed: No (in .gitignore)

---

*Structure analysis: 2026-04-14*
