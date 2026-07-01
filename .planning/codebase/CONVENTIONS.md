# Codebase Map: Conventions

Generated: 2026-07-01

## Summary

Fluxo uses conventional C# naming, WPF with MVVM, Microsoft dependency injection, EF Core repositories, and explicit data-operation boundaries. Prefer an existing domain service, helper, converter, or control over adding another abstraction.

## C# Style

- Projects enable nullable reference types and implicit usings; WPF projects target `net10.0-windows` (`Fluxo/Fluxo.csproj`, `Fluxo.Core/Fluxo.Core.csproj`).
- Source normally uses file-scoped namespaces, four-space indentation, and braces on new lines, as in `Fluxo.Data/Repositories/TransactionRepository.cs`.
- Public types and members use PascalCase; interfaces use an `I` prefix; private instance fields use `_camelCase`; constants use PascalCase.
- Async APIs end in `Async` and normally accept an optional trailing `CancellationToken`, for example `Fluxo.Services/Persistence/TransactionService.cs`.
- Services and repositories are often `sealed` and use primary constructors when construction is only dependency capture. Longer initialization or test seams use explicit constructors, as in `Fluxo/Services/Dialogs/DialogService.cs`.
- Small immutable results and state carriers use records, while EF entities and DTOs are mutable sealed classes (`Fluxo/ViewModels/Shell/Main/DateRangeResolver.cs`, `Fluxo.Core/Entities/Transaction.cs`, `Fluxo.Core/DTO/TransactionDto.cs`).
- Guard clauses use `ArgumentNullException.ThrowIfNull`, `ArgumentException.ThrowIfNullOrWhiteSpace`, and early returns. Invalid enum/state branches normally throw `InvalidOperationException`.
- Collection expressions (`[]`, `[item]`) and expression-bodied members are common when they remain short.

## Naming And File Layout

- One primary type per file is the norm; file name matches the type. Partial view models may split a focused concern, such as `Fluxo/ViewModels/Entities/SavingGoalVM.Progress.cs`.
- View models use the `VM` suffix (`MainVM`, `AccountDetailVM`); views use semantic WPF names such as `Popup`, `Page`, `Panel`, or `Control`.
- Event handlers use `On...`; boolean members use `Is...`, `Has...`, `Can...`, `Should...`, or `Are...`.
- Test methods usually use `Member_Scenario_ExpectedResult`, though some installer tests use `Returns..._When_...` (`Fluxo.Tests/Installer/DotNetRuntimeDetectorTests.cs`).
- Domain code is grouped by feature below `Fluxo.Core`, `Fluxo.Data`, `Fluxo.Services`, `Fluxo/ViewModels`, and `Fluxo/Views`; shared WPF controls, converters, styles, and messages live in `Fluxo.Resources`.

## MVVM And Messaging

- View models derive from CommunityToolkit.Mvvm `ObservableObject` or `ObservableRecipient`; generated properties use `[ObservableProperty]`, and generated commands use `[RelayCommand]`.
- Observable classes must be `partial`. Backing fields use `_camelCase`; generated public properties retain PascalCase.
- Cross-view notifications use `IMessenger`/`WeakReferenceMessenger` and message records under `Fluxo.Resources/Resources/Messages/`. Recipient view models implement `IRecipient<T>` and usually accept an optional messenger for tests.
- Commands and mutable form state belong in view models. Code-behind remains substantial for WPF-only behavior: focus, animation, dispatcher timing, window ownership, drag/chrome, and popup lifecycle.
- Mapping boundaries are explicit: entities to DTOs in `Fluxo.Services/Mappings/EntityDtoProfile.cs`, DTOs to view models in `Fluxo/Mappings/DtoViewModelProfile.cs`.

## WPF And XAML

- Shared resource dictionaries live in `Fluxo.Resources/Resources/`; styles are split by concern under `Fluxo.Resources/Resources/Styles/`.
- Reusable controls and dependency properties live under `Fluxo.Resources/CustomControls/` and `Fluxo.Resources/Components/`; converters live under `Fluxo.Resources/Converters/`.
- Use `StaticResource` for established brushes, icons, converters, and styles rather than duplicating values inline.
- Editable inputs explicitly use `Mode=TwoWay`; text and numeric inputs commonly add `UpdateSourceTrigger=PropertyChanged` (`Fluxo/Views/Popups/AddNewTransaction.xaml`).
- Bindings to read-only/computed view-model properties must explicitly use `Mode=OneWay`. Existing regression coverage is in `Fluxo.Tests/Views/Popups/ProgressBarBindingModeTests.cs`.
- Commands use `{Binding ...Command}`. Visual state selectors may use `DataTrigger`, style setters, or converter-backed bindings according to the existing control template.
- Named template parts use `PART_...`; ordinary named controls use descriptive PascalCase `x:Name` values.

## Dependency Injection And Lifetimes

- Registration stays in `IServiceCollection` extensions: data in `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`, presentation/UI in `Fluxo/Extensions/ServiceCollectionExtensions.cs`.
- EF context, unit of work, and repositories are scoped. Stateless domain services are usually transient. shell state, app-wide services, messenger, and `MainWindow` are singletons.
- `IDataOperationScopeFactory` and `IDataOperationRunner` are singleton gateways that create scopes per operation; do not inject a scoped repository into a singleton/view model to bypass them.
- Modal workflows that need scoped dependencies create a scope in `DialogService` and resolve or construct the popup/view model within that scope.

## Data Access

- UI and service boundaries use `IDataOperationRunner`; the callback receives `IDataOperationScope`, then accesses repositories through `scope.UnitOfWork` (`Fluxo.Services/Persistence/TransactionService.cs`).
- Queries are async and pass cancellation tokens through to EF Core. State changes call repository `Add`, `Update`, or `Remove`, then explicitly call `SaveChangesAsync`.
- Read queries favor `AsNoTracking` or `AsNoTrackingWithIdentityResolution`; related entities are explicitly included where mapping needs them.
- Current transaction persistence uses the unified `Transaction` entity/table. Soft deletion uses `Transaction.IsForDeletion`; notifications use `IsCleared`.
- Schema changes belong in migrations under `Fluxo/Migrations/`; model configuration belongs in `Fluxo.Data/Context/FluxoDbContext.cs`.

## Validation

- Trust-boundary and form validation is explicit. View models commonly use `System.ComponentModel.DataAnnotations.ValidationResult` helpers and surface user-facing messages (`Fluxo/ViewModels/Popups/AddAccountVM.cs`).
- Normalize user strings before persistence and reject empty, overlong, control-character, or domain-invalid values.
- Money remains `decimal`; SQLite money columns are configured as `NUMERIC` and are covered by schema tests.

## Error Handling And Logging

- `DataOperationRunner` rethrows cancellation and existing `DataOperationException`; unexpected exceptions are logged and wrapped once in `DataOperationException` with a user-facing log-file message (`Fluxo.Data/Operations/DataOperationRunner.cs`).
- Do not swallow `OperationCanceledException` as a failure. Best-effort startup/cleanup paths may catch and log warnings while preserving a safe fallback.
- App-wide logging uses `ILogService` where injected and `FluxoLogManager` in static/startup/UI paths. Serilog routes EF/database, issue, and other events to category folders under `%LOCALAPPDATA%/fluxo/logs` (`Fluxo.Services/Logging/FluxoLogService.cs`).
- User-visible failures go through `IDialogService`, `FluxoMessageBox`, toast/result objects, or a view-model error message. Detailed exceptions stay in logs; UI messages reference the current log file.
- Global dispatcher, AppDomain, and unobserved-task handlers are registered in `Fluxo/App.xaml.cs`. Logging shutdown is deliberately non-throwing.
- Installer code is separate from app logging and converts failures into explicit installer state/status, exit codes, and rollback messages (`Fluxo.Installer/BootstrapperEntry.cs`, `Fluxo.Installer/ViewModels/InstallerViewModel.cs`).

## Change Guidance

- Reuse existing services, controls, converters, messages, and test support before adding new helpers.
- Keep changes in the owning feature/layer; avoid moving WPF platform behavior into domain services.
- Add one focused regression test for non-trivial behavior. For XAML-only contracts, follow existing source/XDocument tests; for behavior, prefer calling the helper/view model/repository directly.
- Preserve explicit `Mode=OneWay` on every binding to a read-only property.
