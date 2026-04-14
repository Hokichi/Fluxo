# Coding Conventions

**Analysis Date:** 2026-04-14

## Code Style

**Language & Framework:**
- C# with .NET 10.0 Windows
- WPF for desktop UI
- File-scoped namespaces (`namespace Fluxo.ViewModels;`)
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Implicit usings enabled (`<ImplicitUsings>enable</ImplicitUsings>`)

**Formatting:**
- No explicit linting config (`.editorconfig` or `Directory.Build.props` not present)
- 4-space indentation (inferred from code)
- Consistent brace style: opening braces on same line (C# standard)
- Sealed classes used for security/performance: `public sealed class Expense`

**Code Organization:**
- Classes are organized with properties first, then methods
- Auto-properties with backing fields for state management
- Private fields prefixed with underscore: `private readonly MainVM _mainViewModel;`

## Naming Patterns

**Files:**
- PascalCase for all files: `ExpenseDetailVM.cs`, `BoolToVisibilityConverter.cs`
- Suffixes used consistently:
  - `VM` for ViewModels: `MainVM.cs`, `ExpenseDetailVM.cs`
  - `Repository` for data access: `ExpenseLogRepository.cs`
  - `Converter` for value converters: `BoolToVisibilityConverter.cs`
  - `Service` for business logic: `ExpenseCleanupService`
  - No plurals in collection types, naming based on responsibility

**Classes & Types:**
- PascalCase for class names: `MainVM`, `ExpenseLog`, `IUnitOfWork`
- Interface prefix `I`: `IRepository<T>`, `IExpenseRepository`
- Abstract base classes: `Repository<T>` (non-interface base)
- Generic type parameters with `T` prefix for templates: `TViewModel`, `TExpenseLogViewModel`

**Properties:**
- PascalCase: `public int Id { get; set; }`
- Backing fields with underscore prefix: `private string _nameText = string.Empty;`
- ObservableProperties marked with `[ObservableProperty]` attribute (MVVM Toolkit)
- Initialize string properties to `string.Empty`, not `null`

**Methods:**
- PascalCase: `LoadFromSavedState()`, `ReloadChoicesFromMainViewModel()`
- Private methods with underscore: `_savedState` (field), no prefix on methods themselves
- Async methods suffixed with `Async`: `GetByIdAsync()`, `SaveChangesAsync()`
- Query methods prefixed with verbs: `Get*`, `Find*`, `Query*`

**Variables & Parameters:**
- camelCase for local variables: `var expenseLog`, `var unitOfWork`
- Constants in PascalCase in constants classes: `UserSettingNames.IsFirstRun`
- Protected/internal fields use underscore: `private bool _isInitialized;`

**Enums:**
- PascalCase for enum names and values: `ExpenseCategory.Needs`, `MainContentViewMode.Daily`
- Enums stored in `Fluxo.Core/Enums/` directory

## MVVM & UI Patterns

**ViewModel Base Class:**
- Inherit from `ObservableRecipient` (Community Toolkit MVVM) or `ObservableObject`
- Use `[ObservableProperty]` attributes for binding properties: `[ObservableProperty] private string _username;`
- Generates backing fields with underscore prefix
- Partial classes for messenger recipients: `public partial class MainVM : ObservableRecipient`

**Data Binding:**
- Two-way binding for editable properties
- ICollectionView for filtered/sorted collections: `ICollectionView _invest = CollectionViewSource.GetDefaultView(...)`
- ObservableCollection for dynamic lists: `ObservableCollection<ExpenseLogVM>`

**Commands:**
- Use `[RelayCommand]` attribute from Community Toolkit: `[RelayCommand] private void ClearSelectedTag()`
- Async commands with `[RelayCommand] private async Task Method()`
- Parameters passed directly: `[RelayCommand] private void SetSelectedMainContentView(MainContentViewMode viewMode)`

**Change Notifications:**
- Partial method `OnPropertyChanged(...)` override on demand
- Partial methods on property change: `partial void OnIsEditingChanged(bool value)`
- Manual notification as fallback: `OnPropertyChanged(nameof(IsDailyViewSelected))`

## Repository & Data Access Patterns

**Generic Repositories:**
- Base class `Repository<T>` at `Fluxo.Data.Repositories.Repository.cs`
- Typed repository implementations: `public sealed class ExpenseLogRepository : Repository<ExpenseLog>`
- Interfaces in layers: `IRepository<T>`, `IReadRepository<T>`, `IWriteRepository<T>`
- Specialized repositories: `IExpenseRepository`, `IExpenseLogRepository` extending base interfaces

**Repository Methods:**
- Standard CRUD: `GetAllAsync()`, `GetByIdAsync(int id)`, `AddAsync(T entity)`, `DeleteAsync(T entity)`
- Query variants: `GetByDayAsync()`, `GetByWeekAsync()`, `GetByMonthAsync()`, `GetByCategoryAsync()`
- Async first, CancellationToken parameter last with default
- Return types: `Task<IReadOnlyList<T>>` for collections, `Task<T?>` for single items

**Unit of Work Pattern:**
- Central `IUnitOfWork` interface in `Fluxo.Core/Interfaces/`
- Implemented at `Fluxo.Data/UnitOfWork.cs`
- Factory pattern for transient creation: `Func<IUnitOfWork> unitOfWorkFactory`
- Separate read/write units: `IViewModelReadUnitOfWork`, `IViewModelWriteUnitOfWork`

**EF Core Configuration:**
- DbContext: `FluxoDbContext.cs` in `Fluxo.Data/Context/`
- Model configuration in `OnModelCreating()` with helper methods: `ConfigureExpense()`
- Sealed entities: `public sealed class Expense`
- SQLite with migrations in main project: `Fluxo/Migrations/`

## Error Handling

**Exception Throwing:**
- Specific exception types when appropriate: `InvalidOperationException`, `NotImplementedException`
- Messages included with context: `throw new InvalidOperationException($"Unable to find spending source {spendingSourceId}.");`
- Converters throw on invalid parameters: `throw new Exception("Invalid Parameters")`
- Unimplemented methods: `throw new NotImplementedException();`

**Try-Catch Patterns:**
- Minimal try-catch, mostly at application boundary (App.xaml.cs)
- Specific exception handling in converters: `catch (FormatException)`, `catch (NotSupportedException)`
- General exception handling at app startup only
- No empty catch blocks observed; exceptions either specific or propagated

**Async/Await:**
- All repository operations return `Task<T>` or `Task`
- CancellationToken parameter standard: `CancellationToken cancellationToken = default`
- Async void only in event handlers: `async void OnStartup()`, `async Task RunSetupWizardAsync()`

## Dependency Injection

**ServiceCollection Extension Pattern:**
- Extension methods in `ServiceCollectionExtensions.cs`: `AddFluxoPresentation()`, `AddFluxoData()`
- File location: `Fluxo/Extensions/ServiceCollectionExtensions.cs`, `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`
- Services registered by interface: `services.AddTransient<IUnitOfWork>(...)`
- Lifetime management: Transient for repositories, Singleton for MainVM, Singletons for singleton collections

**Service Resolution:**
- IServiceProvider for runtime resolution
- GetRequiredService<T>() with null-safety: `serviceProvider.GetRequiredService<MainVM>()`

## Comments & Documentation

**XML Documentation:**
- Used sparingly for public APIs and class summaries
- Class summary: `/// <summary>\n/// Interaction logic for App.xaml\n/// </summary>`
- Method summaries for interface implementations: mostly on public members
- Auto-generated migrations include `/// <inheritdoc />`

**Inline Comments:**
- Minimal inline comments; code is self-documenting via naming
- Comments for non-obvious behavior: `// MainWindow is Singleton but needs fresh IUnitOfWork (Transient) per popup...`
- Design decisions documented when implementation differs from standard patterns

**Regions:**
- No visible use of `#region`/`#endregion` blocks in codebase
- Organization by class structure: constructors, properties, methods

## Class Structure

**Typical File Layout:**
1. Using statements (organized by System, namespaces, then project namespaces)
2. File-scoped namespace declaration
3. Class declaration with inheritance/interfaces
4. Private fields (underscored)
5. Observable properties with [ObservableProperty]
6. Public properties (calculated/computed)
7. Constructor(s)
8. Partial methods (change notifications)
9. Public methods
10. Private/helper methods
11. Event handlers

## Sealing & Access Modifiers

**Sealed Classes:**
- Used throughout entity and repository implementations
- Reason: performance (no virtual dispatch) and design closure
- Examples: `public sealed class Expense`, `public sealed class ExpenseLogRepository`

**Access Levels:**
- `public` for interfaces, service entry points
- `internal` for cross-project but not public APIs
- `private` default for all fields and internal methods
- Rare use of `protected` (most base functionality sealed)

## Nullability

**Null Handling:**
- Nullable reference types enabled project-wide
- Null-coalescing default: `string.Empty` for string properties
- Null-safe operators: `value is bool b && b`, pattern matching
- `??` operator for fallback: `existingSetting ?? defaultValue`
- Explicit nullability in return types: `Task<T?>` vs `Task<T>`

