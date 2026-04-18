# Codebase Structure

**Analysis Date:** 2026-04-18

## Directory Layout

```
Fluxo/                                  # Repository root (worktree)
├── Fluxo.slnx                          # Solution file (XML solution format)
├── .gitignore
├── .gitattributes
├── docs/                               # External docs (currently `superpowers/`)
├── .planning/                          # GSD planning artifacts (codebase docs, plans)
├── .claire/, .claude/                  # Tooling/agent state (do not modify by hand)
│
├── Fluxo.Core/                         # Domain contracts (no project deps)
│   ├── Fluxo.Core.csproj
│   ├── Constants/UserSettingNames.cs
│   ├── DTO/                            # *Dto POCOs
│   ├── Entities/                       # EF entity POCOs
│   ├── Enums/
│   ├── Filters/                        # Search filter objects
│   ├── Interfaces/
│   │   ├── IUnitOfWork.cs
│   │   ├── Repositories/
│   │   └── Services/
│   └── Exceptions/                     # (folder reserved, empty)
│
├── Fluxo.Data/                         # EF Core 10 (SQLite) persistence
│   ├── Fluxo.Data.csproj
│   ├── Configurations/                 # (folder reserved, empty)
│   ├── Context/
│   │   ├── FluxoDbContext.cs
│   │   └── FluxoDbContextFactory.cs
│   ├── Extensions/ServiceCollectionExtensions.cs   # AddFluxoData()
│   ├── Migrations/                     # Initial migrations + snapshot
│   ├── Repositories/
│   │   ├── Repository.cs               # Generic base
│   │   └── *Repository.cs              # Per-aggregate repos
│   └── UnitOfWork.cs
│
├── Fluxo.Services/                     # Application services (Entity↔DTO)
│   ├── Fluxo.Services.csproj
│   ├── Calculations/                   # (folder reserved, empty)
│   ├── Extensions/                     # (folder reserved, empty)
│   ├── Mappings/EntityDtoProfile.cs
│   └── Persistence/
│       ├── ExpenseService.cs
│       ├── ExpenseLogService.cs
│       ├── SpendingSourceService.cs
│       └── TagService.cs
│
├── Fluxo/                              # WPF presentation + composition root
│   ├── Fluxo.csproj                    # OutputType=WinExe, TargetFramework=net10.0-windows
│   ├── App.xaml                        # Resource dictionaries, fonts, converters
│   ├── App.xaml.cs                     # DI bootstrap + OnStartup
│   ├── AssemblyInfo.cs
│   ├── Converters/                     # IValueConverter implementations
│   ├── Extensions/ServiceCollectionExtensions.cs   # AddFluxoPresentation() + AddUIData()
│   ├── Mappings/DtoViewModelProfile.cs
│   ├── Migrations/                     # EF migrations live here (MigrationsAssembly = "Fluxo")
│   │   └── FluxoDesignTimeDbContextFactory.cs
│   ├── Resources/
│   │   ├── icon.ico
│   │   ├── Theme.xaml
│   │   ├── Icons.xaml
│   │   ├── Fonts/                      # SFT Schrifted Round TRIAL family + ContainerStyles.xaml
│   │   ├── Styles/                     # ButtonStyles, GlobalStyles, MainWindowStyles, PopupStyles, SettingsStyle, StartupWizardStyle, TextBoxStyles
│   │   └── CustomControls/             # BasePopup, BalloonBorder, FluxoMessageBox, MoneyTextBox, IPopupHost, etc.
│   ├── Services/
│   │   ├── Dialogs/                    # IDialogService + DialogService
│   │   └── History/                    # LogMemoryManager + LogMemoryActions (undo/redo)
│   ├── ViewModels/
│   │   ├── Controls/                   # Control-scoped VMs (DayOfWeekVM)
│   │   ├── Entities/                   # *VM mirroring DTOs (ExpenseVM, etc.)
│   │   ├── Helpers/                    # Pure helpers (MonthlyDueDateHelper)
│   │   ├── Messages/                   # CommunityToolkit messenger payloads
│   │   ├── Notifications/              # NotificationItemVM
│   │   ├── Popups/                     # Per-popup VMs (QuickAddVM, SettingsVM, ...)
│   │   └── Shell/
│   │       ├── MainVM.cs
│   │       ├── MainVM.ExpenseDetailMessenger.cs
│   │       ├── MainContentViewMode.cs
│   │       └── Main/                   # Long-lived shell-region VMs
│   └── Views/
│       ├── Components/                 # Reusable user controls (DateSelector, ExpensesList, IncomeSource)
│       ├── Popups/                     # Modal popup windows + Settings/Tabs/
│       └── Shell/
│           ├── StartupLoaderPopup.xaml(.cs)
│           ├── Main/                   # MainWindow + Controls/ + Sections/
│           └── Wizard/                 # StartupWizardPopup + Pages/Steps/
│
└── Fluxo.Tests/                        # xUnit tests (mirrors Fluxo namespaces)
    ├── Fluxo.Tests.csproj
    ├── Services/Dialogs/
    ├── ViewModels/
    │   ├── Popups/
    │   └── Shell/Main/
    └── Views/Shell/Main/
```

## Directory Purposes

**`Fluxo.Core/`:**
- Purpose: Pure domain layer — entities, DTOs, enums, filters, and contracts (interfaces). No external runtime deps.
- Key files: `Fluxo.Core/Interfaces/IUnitOfWork.cs`, `Fluxo.Core/Entities/Expense.cs`, `Fluxo.Core/DTO/ExpenseDto.cs`, `Fluxo.Core/Constants/UserSettingNames.cs`

**`Fluxo.Data/Context/`:**
- Purpose: `DbContext` and design/runtime factories
- Key files: `Fluxo.Data/Context/FluxoDbContext.cs` (Fluent API model config + auto-include for reference navigations), `Fluxo.Data/Context/FluxoDbContextFactory.cs`

**`Fluxo.Data/Repositories/`:**
- Purpose: Per-aggregate repository implementations on top of generic `Repository<T>`
- Key files: `Fluxo.Data/Repositories/Repository.cs`, `Fluxo.Data/Repositories/ExpenseRepository.cs`, `SpendingSourceRepository.cs`, `UserSettingsRepository.cs`

**`Fluxo.Data/Migrations/`:**
- Purpose: EF Core migration history for the `Fluxo.Data` project (legacy/initial schema). Note: live runtime migrations now live under `Fluxo/Migrations/` because `MigrationsAssembly("Fluxo")` is configured in both factories.

**`Fluxo.Services/Persistence/`:**
- Purpose: Application services that orchestrate `IUnitOfWork` operations and Entity↔DTO mapping
- Key files: `Fluxo.Services/Persistence/ExpenseService.cs`, `ExpenseLogService.cs`, `SpendingSourceService.cs`, `TagService.cs`

**`Fluxo/Views/`:**
- Purpose: All XAML views; separated by role
  - `Components/` — reusable user controls injected into windows
  - `Popups/` — modal dialogs (each is a `Window` derivative or `BasePopup`); `Settings/Tabs/` for the multi-tab settings popup
  - `Shell/Main/` — `MainWindow` plus `Sections/` (panels) and `Controls/` (small region-specific controls)
  - `Shell/Wizard/` — first-run wizard plus `Pages/Steps/` for ordered step pages

**`Fluxo/ViewModels/`:**
- Purpose: VM classes mirroring view organization. `Shell/Main/` shell-region VMs are singletons; `Popups/` and `Entities/` VMs are transient (see `Fluxo/Extensions/ServiceCollectionExtensions.cs`)

**`Fluxo/Services/`:**
- Purpose: UI-only services (`Dialogs/` for popup factory, `History/` for undo/redo). Distinct from `Fluxo.Services` project — these never touch persistence directly except via `IUnitOfWork`.

**`Fluxo/Resources/`:**
- Purpose: WPF resource dictionaries. `App.xaml` merges `Theme.xaml`, `Icons.xaml`, all of `Styles/*.xaml`, and `Fonts/ContainerStyles.xaml`. `CustomControls/` holds shared popup/control primitives.

**`Fluxo/Migrations/`:**
- Purpose: Active EF Core migrations + `FluxoDesignTimeDbContextFactory.cs` for `dotnet ef` tooling. Both runtime and design-time factories declare `MigrationsAssembly("Fluxo")`.

**`Fluxo.Tests/`:**
- Purpose: xUnit test project. Mirrors source namespaces (`ViewModels/Shell/Main/`, `Services/Dialogs/`, `Views/Shell/Main/`).

## Key File Locations

**Entry Points:**
- `Fluxo/App.xaml.cs`: WPF application bootstrap, DI composition, first-run logic
- `Fluxo/Views/Shell/Main/MainWindow.xaml.cs`: Primary shell window
- `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml.cs`: First-run wizard

**Configuration:**
- `Fluxo.slnx`: Solution
- `Fluxo/Fluxo.csproj`: WPF executable, packages (`AutoMapper`, `CommunityToolkit.Mvvm`, `MahApps.Metro.IconPacks`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Hosting`, `Newtonsoft.Json`, `Serilog`, `Serilog.Sinks.File`)
- `Fluxo.Data/Fluxo.Data.csproj`: EF Core 10 + SQLite packages
- `Fluxo.Services/Fluxo.Services.csproj`: AutoMapper, FluentValidation, Toolkit.Uwp.Notifications
- `Fluxo.Tests/Fluxo.Tests.csproj`: xUnit + Microsoft.NET.Test.Sdk
- `Fluxo/App.xaml`: Application resource dictionary registrations (fonts, converters, themes, styles)

**DI Composition:**
- `Fluxo/Extensions/ServiceCollectionExtensions.cs`: `AddFluxoPresentation()` (services + AutoMapper) and `AddUIData()` (VMs, popups, dialog/messenger)
- `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`: `AddFluxoData()` (DbContext, repositories, UoW factory)

**Core Logic:**
- `Fluxo/ViewModels/Shell/MainVM.cs`: Dashboard state, notifications, undo coordination
- `Fluxo.Services/Persistence/ExpenseService.cs`: Reference example of write-flow service
- `Fluxo.Data/Repositories/Repository.cs`: Generic CRUD base with safe tracking semantics
- `Fluxo.Data/UnitOfWork.cs`: Aggregates repositories around a single `DbContext`
- `Fluxo.Data/Context/FluxoDbContext.cs`: Fluent model configuration + reference auto-include

**Persistence:**
- `Fluxo.Data/Context/FluxoDbContextFactory.cs`: Builds runtime DbContext using `fluxo.db` next to `AppContext.BaseDirectory`
- `Fluxo/Migrations/`: Active migrations and snapshot
- `Fluxo/Migrations/FluxoDesignTimeDbContextFactory.cs`: Design-time factory for `dotnet ef migrations add ...`

**Mapping:**
- `Fluxo.Services/Mappings/EntityDtoProfile.cs`: Entity ⇄ DTO
- `Fluxo/Mappings/DtoViewModelProfile.cs`: DTO ⇄ ViewModel

**Testing:**
- `Fluxo.Tests/ViewModels/Shell/Main/`: VM tests (e.g., `BudgetAllocationPanelVMTests.cs`, `DateRangeResolverTests.cs`)
- `Fluxo.Tests/Services/Dialogs/DialogServiceTests.cs`
- `Fluxo.Tests/Views/Shell/Main/MainWindowShortcutMatcherTests.cs`

## Naming Conventions

**Files:**
- C# classes: `PascalCase.cs` matching class name (`ExpenseService.cs`, `MonthlyDueDateHelper.cs`)
- Interfaces: `I` prefix (`IExpenseService.cs`, `IUnitOfWork.cs`, `IDialogService.cs`)
- Generic base + concrete pairings: `Repository<T>` ↔ `ExpenseRepository`
- ViewModels: suffix `VM` (`MainVM.cs`, `ExpenseLogVM.cs`, `AddSpendingSourceVM.cs`)
- Partial-class extension: `<ClassName>.<Aspect>.cs` (e.g., `MainVM.ExpenseDetailMessenger.cs`, `SavingGoalVM.Progress.cs`)
- DTOs: suffix `Dto` (`ExpenseDto.cs`)
- Filters: suffix `Filter` (`ExpenseFilter.cs`)
- Messages: descriptive name + `Message` suffix (`DashboardDataInvalidatedMessage.cs`, `UsernameChangedMessage.cs`)
- WPF view + code-behind: `*.xaml` + `*.xaml.cs` pair
- Popups: suffix `Popup` (`AddSpendingSourcePopup.xaml`, `SettingsPopup.xaml`)
- Migrations: `<UTC-yyyyMMddHHmmss>_<Description>.cs` (auto-generated by `dotnet ef`)

**Directories:**
- Plural for collections of similar artifacts: `Entities/`, `Repositories/`, `Services/`, `ViewModels/`, `Views/`, `Popups/`, `Pages/`
- Singular when describing a role/aspect: `Context/`, `Shell/`, `Main/`, `History/`, `Wizard/`
- One directory per architectural concern (e.g., `Mappings/`, `Migrations/`, `Converters/`)

**Namespaces:** Follow folder structure exactly (e.g., `Fluxo.Views.Shell.Main`, `Fluxo.Services.Persistence`, `Fluxo.Core.Interfaces.Repositories`).

## Where to Add New Code

**New domain entity:**
- Entity POCO: `Fluxo.Core/Entities/<Name>.cs`
- DTO: `Fluxo.Core/DTO/<Name>Dto.cs`
- Repository contract: `Fluxo.Core/Interfaces/Repositories/I<Name>Repository.cs`
- Repository implementation: `Fluxo.Data/Repositories/<Name>Repository.cs` (extend `Repository<T>`)
- Register in `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs` (also add to the `IUnitOfWork` factory and to `Fluxo.Data/UnitOfWork.cs`)
- Add `DbSet` + Fluent config to `Fluxo.Data/Context/FluxoDbContext.cs`
- Map in both AutoMapper profiles (`Fluxo.Services/Mappings/EntityDtoProfile.cs`, `Fluxo/Mappings/DtoViewModelProfile.cs`)
- Add migration: run `dotnet ef migrations add <Name>` from the `Fluxo` project (migrations land in `Fluxo/Migrations/`)

**New application service:**
- Contract: `Fluxo.Core/Interfaces/Services/I<Name>Service.cs`
- Implementation: `Fluxo.Services/Persistence/<Name>Service.cs` (constructor-inject `IUnitOfWork` + `IMapper`)
- Register transient in `Fluxo/Extensions/ServiceCollectionExtensions.cs::AddFluxoPresentation`

**New popup/dialog:**
- View: `Fluxo/Views/Popups/<Name>Popup.xaml(.cs)` (inherit `Window` or extend `BasePopup`)
- VM: `Fluxo/ViewModels/Popups/<Name>VM.cs`
- Register both in `Fluxo/Extensions/ServiceCollectionExtensions.cs::AddUIData` (transient)
- Expose via `Fluxo/Services/Dialogs/IDialogService.cs` + implementation

**New shell panel/section:**
- View: `Fluxo/Views/Shell/Main/Sections/<Name>Panel.xaml(.cs)`
- VM: `Fluxo/ViewModels/Shell/Main/<Name>PanelVM.cs` (typically singleton)
- Wire into `MainWindow.xaml` via a host element and assign `DataContext` in `MainWindow.xaml.cs`

**New value converter:**
- Place in `Fluxo/Converters/`
- Register as a resource in the converters `ResourceDictionary` inside `Fluxo/App.xaml`

**New custom control / popup primitive:**
- Place in `Fluxo/Resources/CustomControls/`

**New EF migration:**
- Generated under `Fluxo/Migrations/` (see `Fluxo/Migrations/FluxoDesignTimeDbContextFactory.cs`)

**New test:**
- Mirror source path under `Fluxo.Tests/` (e.g., new `Fluxo/ViewModels/Shell/Main/FooVM.cs` → `Fluxo.Tests/ViewModels/Shell/Main/FooVMTests.cs`)

**New cross-VM message:**
- Add a sealed class in `Fluxo/ViewModels/Messages/<Name>Message.cs`
- Send via `WeakReferenceMessenger.Default.Send(...)`; subscribe in VM constructor with `WeakReferenceMessenger.Default.Register<TRecipient, TMessage>(...)`

## Special Directories

**`Fluxo/Migrations/`:**
- Purpose: Active EF Core migration set used at runtime (`MigrationsAssembly("Fluxo")` in `FluxoDbContextFactory`)
- Generated: Yes (`dotnet ef migrations add ...`); manual edits discouraged
- Committed: Yes

**`Fluxo.Data/Migrations/`:**
- Purpose: Earlier migration set living in the persistence project (kept for history)
- Generated: Yes
- Committed: Yes

**`Fluxo/Resources/Fonts/`:**
- Purpose: TTF font assets for the SFT Schrifted Round TRIAL family, registered as `<Resource Include="..." />` in `Fluxo/Fluxo.csproj` and exposed as `FontFamily` resources in `Fluxo/App.xaml`
- Generated: No
- Committed: Yes

**`Fluxo.Core/Exceptions/`, `Fluxo.Data/Configurations/`, `Fluxo.Services/Calculations/`, `Fluxo.Services/Extensions/`:**
- Purpose: Reserved (declared via `<Folder Include="..." />` in their respective `.csproj` files); empty in the current snapshot. Use when adding custom exceptions, EF entity-type configurations, calculation utilities, or service-layer extension methods respectively.
- Generated: No
- Committed: Yes (kept by `<Folder Include>` declarations)

**`.planning/`:**
- Purpose: GSD orchestration artifacts including this `codebase/` directory of analysis docs
- Generated: Yes (by GSD commands)
- Committed: Yes

**`docs/`:**
- Purpose: Long-form documentation (`docs/superpowers/`)
- Generated: No
- Committed: Yes

---

*Structure analysis: 2026-04-18*
