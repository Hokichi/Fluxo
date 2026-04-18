# Coding Conventions

**Analysis Date:** 2026-04-18

## Naming Patterns

**Files:**
- One public type per file, file name matches the type. Examples: `Fluxo/ViewModels/Shell/Main/BudgetAllocationPanelVM.cs`, `Fluxo.Data/Repositories/ExpenseRepository.cs`.
- ViewModels use the `VM` suffix (not `ViewModel`). Examples: `Fluxo/ViewModels/Popups/StartupWizardVM.cs`, `Fluxo/ViewModels/Shell/MainVM.cs`.
- Partial ViewModel slices use a dotted file name: `Fluxo/ViewModels/Shell/MainVM.cs` and `Fluxo/ViewModels/Shell/MainVM.ExpenseDetailMessenger.cs`.
- Test files mirror the source class with a `Tests` suffix: `Fluxo.Tests/ViewModels/Shell/Main/BudgetAllocationPanelVMTests.cs` covers `Fluxo/ViewModels/Shell/Main/BudgetAllocationPanelVM.cs`.
- Interface files start with `I` and live in `Fluxo.Core/Interfaces/...`. Example: `Fluxo.Core/Interfaces/Repositories/IExpenseRepository.cs`.

**Folders:**
- PascalCase, plural for collections of types (`Repositories`, `Services`, `Entities`, `Enums`, `Filters`, `Messages`, `Popups`).
- Tests mirror the source folder layout under `Fluxo.Tests/` (e.g. `Fluxo.Tests/ViewModels/Shell/Main/`).

**Types:**
- `PascalCase` for classes, records, interfaces, enums, and methods.
- DTOs end with `Dto` (`Fluxo.Core/DTO/ExpenseDto.cs`).
- Entities are bare nouns (`Fluxo.Core/Entities/Expense.cs`).
- Filters end with `Filter` (`Fluxo.Core/Filters/ExpenseFilter.cs`).
- Messages end with `Message` and live in `Fluxo/ViewModels/Messages/` (`ViewModeChangeMessage`, `DateRangeSelectionChangedMessage`).
- Snapshot DTO records use the `Snapshot` suffix and are declared as `public sealed record` next to the consuming VM (`BudgetAllocationPanelSnapshot` in `Fluxo/ViewModels/Shell/Main/BudgetAllocationPanelVM.cs`).

**Methods:**
- Async methods end with `Async` and accept an optional `CancellationToken cancellationToken = default`. Example: `Task<IReadOnlyList<ExpenseDto>> GetAllAsync(CancellationToken cancellationToken = default)` in `Fluxo.Services/Persistence/ExpenseService.cs`.
- Command-bound methods are wired via `CommunityToolkit.Mvvm.Input` source generators; method names are imperative (`NavigatePrevious`, `LoadSnapshot`, `GoNextAsync`).

**Fields and parameters:**
- Private instance fields use `_camelCase` (`_unitOfWork`, `_mapper`, `_reloadGate`).
- `private readonly` fields are preferred for injected dependencies.
- Static `PropertyInfo` and constants use `PascalCase` (`IdProperty` in `Fluxo.Data/Repositories/Repository.cs`; `DefaultCurrencyCode` in `Fluxo/ViewModels/Popups/StartupWizardVM.cs`).
- `[ObservableProperty]` private backing fields use `_camelCase` and are exposed by the source generator as `PascalCase` properties.
- Parameters are `camelCase`.

**Constants:**
- Strings used as keys go in static `public const string` members of `Fluxo.Core/Constants/UserSettingNames.cs`, defined with `nameof(...)` so the key matches the identifier.

## Code Style

**Language and platform:**
- C# (latest, implied by `net10.0-windows` target in every `.csproj`).
- All projects enable `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.
- File-scoped namespaces are used everywhere (`namespace Fluxo.ViewModels.Shell;`).

**Formatting:**
- 4-space indentation.
- Allman braces for types and methods; `if (...) statement;` style without braces is allowed for one-line guards (`if (popup.Owner is null) popup.Owner = owner;` in `Fluxo/Services/Dialogs/DialogService.cs`).
- Trailing commas in multi-line collection expressions (`[ ... ]`).

**Linting / formatters:**
- No `.editorconfig`, `.ruleset`, StyleCop, or analyzers config files are present.
- EF Core analyzers are referenced via `Microsoft.EntityFrameworkCore.Analyzers` in `Fluxo.Data/Fluxo.Data.csproj`.

## Modern C# Idioms

- **Primary constructors** for services and the data layer: `Repository<T>(FluxoDbContext dbContext)` in `Fluxo.Data/Repositories/Repository.cs`, `UnitOfWork(...)` in `Fluxo.Data/UnitOfWork.cs`, `ExpenseService(IUnitOfWork unitOfWork, IMapper mapper)` in `Fluxo.Services/Persistence/ExpenseService.cs`.
- **Collection expressions** `[]` for empty/initialized collections (`private readonly HashSet<int> _hiddenSavingGoalIds = [];`).
- **Target-typed `new()`** for property defaults (`public ExpenseTagDto ExpenseTag { get; set; } = new();`).
- **Records** for immutable value carriers: `public sealed record DateRange(DateTime From, DateTime To);` in `Fluxo/ViewModels/Shell/Main/DateRangeResolver.cs`; snapshot records co-located with VMs.
- **Pattern matching and switch expressions** for branching logic (`DateRangeResolver.Resolve`, `is { } trackedEntity` in `Fluxo.Data/Repositories/Repository.cs`).
- **Tuples** with named elements for state pairs (`(DateTime From, DateTime To)? _selectedRange;`).

## Class Design

- Classes are marked `sealed` whenever inheritance is not required (entities, DTOs, services, DialogService, UnitOfWork, AutoMapper profiles, message types, snapshot records).
- ViewModels are `public partial class ... : ObservableObject` or `: ObservableRecipient` (CommunityToolkit MVVM source generators).
- Static helpers are declared `public static class` (`DateRangeResolver`, `MainWindowShortcutMatcher`, `UserSettingNames`).

## Field Ordering Convention

`Fluxo/ViewModels/Shell/MainVM.cs` documents the canonical ordering for ViewModel fields with explicit comments:
1. `private readonly` injected dependencies and shared collections
2. `private` mutable state fields with default values
3. `[ObservableProperty]` backing fields, grouped by feature with section comments (`// Budget Summary`, `// Available`, `// Spent`, etc.).

Constructor(s) follow, then commands/methods.

## Dual Constructor Pattern (testable VMs)

Panel ViewModels expose two constructors so they can run with full DI in production and with no services in tests:

```csharp
public BudgetAllocationPanelVM(
    IExpenseLogService expenseLogService,
    ISpendingSourceService spendingSourceService,
    ITagService tagService,
    IMapper mapper,
    IMessenger? messenger = null)
    : base(messenger ?? WeakReferenceMessenger.Default) { ... }

public BudgetAllocationPanelVM(IMessenger? messenger = null)
    : base(messenger ?? WeakReferenceMessenger.Default) { ... }
```

Service fields are declared as nullable (`IExpenseLogService? _expenseLogService;`) so the parameterless ctor leaves them null. See `Fluxo/ViewModels/Shell/Main/BudgetAllocationPanelVM.cs`, `SavingGoalsPanelVM.cs`, `NotificationPanelVM.cs`.

## Import Organization

**Order observed in source files:**
1. `System.*` namespaces
2. Third-party namespaces (`AutoMapper`, `CommunityToolkit.Mvvm.*`, `Microsoft.*`, `Serilog`)
3. `Fluxo.Core.*`
4. `Fluxo.Services.*` / `Fluxo.Data.*`
5. `Fluxo.*` (presentation: ViewModels, Views, Resources, Converters, Mappings, Services)

A blank line is not used between groups; imports are alphabetized within and across groups by `using` statement text.

**Path / namespace aliases:**
- None. Projects rely on `<ImplicitUsings>` for the BCL globals only.

## Project / Layer Structure

- `Fluxo.Core` — entities, DTOs, enums, filters, constants, repository/service interfaces. No dependencies.
- `Fluxo.Data` — EF Core implementations of repository/UoW interfaces. References `Fluxo.Core`.
- `Fluxo.Services` — `Persistence/` services (DTO-facing) and AutoMapper `Mappings/`. References `Fluxo.Core` and `Fluxo.Data`.
- `Fluxo` — WPF presentation (Views, ViewModels, Converters, Resources, dialog services, App composition). References `Fluxo.Core`, `Fluxo.Data`, `Fluxo.Services`.
- `Fluxo.Tests` — xUnit tests. References `Fluxo`.

Layer dependency rule: presentation → services → data → core. Services do not reference WPF; entities/DTOs do not depend on EF Core.

## Dependency Injection

- DI is configured via extension methods on `IServiceCollection`: `AddFluxoData()` (in `Fluxo.Data/Extensions/`), `AddFluxoPresentation()` and `AddUIData()` (in `Fluxo/Extensions/ServiceCollectionExtensions.cs`).
- Services and repositories are registered `AddTransient`. The `IMapper`, `IMessenger`, `IDialogService`, and the main panel/window ViewModels are `AddSingleton`. Popups and per-item VMs are `AddTransient`.
- Use the `ServiceCollectionExtensions` pattern for new modules; do not register types inline in `App.xaml.cs`.

## Error Handling

**Patterns observed:**
- Validate inputs and throw `InvalidOperationException` with a user-meaningful message for unexpected states. Example in `Fluxo.Services/Persistence/ExpenseService.cs`: `throw new InvalidOperationException($"SpendingSource with id {dto.SpendingSourceId} was not found.");`.
- `DateRangeResolver.Resolve` throws `InvalidOperationException` for `MainContentViewMode.AllTime` and unknown values via the switch-expression default arm.
- Service/repository methods do not catch exceptions; failures bubble up. Top-level handling lives in `Fluxo/App.xaml.cs` `OnStartup`, which catches `Exception`, surfaces it via `IDialogService.ShowError`, and falls back to `FluxoMessageBox.Show` if DI is unavailable.
- `Repository<T>.Update`/`Remove` defensively re-attach the tracked instance instead of throwing, to avoid EF duplicate-tracking conflicts (see comments in `Fluxo.Data/Repositories/Repository.cs`).

**Result-style returns:**
- Some VM operations return a result object instead of throwing. `StartupWizardVM.GoNextAsync()` returns a value with `IsSuccess` checked in tests (`StartupWizardVMTests.GoNextAsync_OnStep1_DoesNotOverwriteExistingSalarySetting`).

## Logging

- **Framework:** `Serilog` + `Serilog.Sinks.File` are referenced in `Fluxo/Fluxo.csproj`, but no `Log.Information/Warning/Error` call sites exist in the source tree. Logging is wired only via the package references; AutoMapper is constructed with `NullLoggerFactory.Instance` in `Fluxo/Extensions/ServiceCollectionExtensions.cs`.
- **Guidance:** When logging is added, prefer Serilog's static `Log` sink and inject `Microsoft.Extensions.Logging.ILogger<T>` only if integrating with hosted services.

## Comments

**When to comment:**
- Explain non-obvious EF Core behavior. See `Fluxo.Data/Repositories/Repository.cs` (e.g. "Use Entry().State instead of DbSet.Update() to avoid recursively attaching navigation properties").
- Explain the order/intent of mutating operations. See `Fluxo.Services/Persistence/ExpenseService.cs` ("Validate source exists before staging any entities.", "Link the log via navigation — EF resolves the FK after insert.").
- Section dividers inside large VMs use `// 1.`, `// 2.`, `// 3.` and feature labels (`// Budget Summary`, `// Available`).

**Doc comments:**
- `///` summary blocks are reserved for framework-required types (`App.xaml.cs` has the auto-generated WPF summary). Public APIs in services/VMs are not generally XML-documented.

## Function Design

- **Async-first** for any I/O. Services and repositories are async with `CancellationToken` parameters defaulting to `default`.
- **Small, single-purpose helpers** are factored out as `private static` methods (`DateRangeResolver.GetStartOfWeek`, `DialogService.ShowDialog`, `App.EnsureFirstRunSettingAsync`).
- **Return types favor `IReadOnlyList<T>`** for query results (see all repositories and `IExpenseService`).
- **Method overloads** are used to vary parameter shape rather than optional bag arguments. Example in `Fluxo/Services/Dialogs/DialogService.cs` where `ShowAddSpendingSource` has both a parameterless overload (DI-resolved popup) and one taking a pre-built VM.

## Module Design

- **Exports:** All cross-project usage is via interfaces in `Fluxo.Core/Interfaces`. Concrete classes are internal-by-namespace but `public` so DI can construct them.
- **Barrel files:** None. Consumers `using Fluxo.Core.Interfaces.Repositories;` etc. directly.
- **MVVM messaging:** `CommunityToolkit.Mvvm.Messaging` `WeakReferenceMessenger` is the canonical cross-VM bus. Define a `sealed class` per message in `Fluxo/ViewModels/Messages/`, deriving from `ValueChangedMessage<T>` when carrying a single value (`ViewModeChangeMessage`).

## AutoMapper Conventions

- One profile per layer pair. `Fluxo.Services/Mappings/EntityDtoProfile.cs` maps Entity ↔ DTO; `Fluxo/Mappings/DtoViewModelProfile.cs` maps DTO ↔ VM with `.ReverseMap()`.
- VMs and DTOs intentionally mirror property names so AutoMapper requires no per-member configuration.

---

*Convention analysis: 2026-04-18*
