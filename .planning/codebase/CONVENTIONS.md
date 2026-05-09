# Coding Conventions

**Analysis Date:** 2026-05-09

## Project Style

- C# projects target `net10.0-windows` with nullable reference types and implicit usings enabled.
- WPF is enabled across the app, resources, core, data, services, installer, and test projects where needed.
- File-scoped namespaces are the dominant style.
- Braces use Allman style with 4-space indentation in C#.
- Modern C# is common: primary constructors, collection expressions, target-typed `new`, records/record structs, pattern matching, and raw string literals.
- No repository-level `.editorconfig`, ruleset, or style configuration was observed; consistency is maintained by convention and compiler/analyzer feedback.

## Naming Patterns

- Types, files, enums, records, and public members use `PascalCase`.
- Interfaces use the `I` prefix (`IDataOperationRunner`, `IExpenseService`, `IUnitOfWork`).
- View models use the `VM` suffix and generally live under `Fluxo/ViewModels/**`.
- Services use `Service`; repositories use `Repository`; message contracts use `Message`.
- Private fields use `_camelCase`.
- Local variables and parameters use `camelCase`.
- Async methods use the `Async` suffix.
- Test classes end in `Tests`; test methods use behavior-oriented names such as `MethodOrScenario_Condition_ExpectedResult`.

## Layering and Boundaries

- `Fluxo.Core` holds entities, DTOs, enums, constants, exceptions, filters, and interfaces.
- `Fluxo.Data` owns EF Core `DbContext`, repository implementations, unit of work, and data operation scope/runner infrastructure.
- `Fluxo.Services` owns persistence, notification, dialog, logging, UI support, history, and AutoMapper service profiles.
- `Fluxo.Resources` owns shared WPF components, custom controls, converters, fonts, icons, styles, theme dictionaries, and message contracts used by the UI.
- `Fluxo` is the WPF shell and presentation layer: app startup, views, view models, AutoMapper presentation profile, and EF migrations.
- `Fluxo.Installer`, `Fluxo.Installer.Msi`, and `Fluxo.Installer.Bundle` contain WPF bootstrapper UI and WiX authoring.

## MVVM Practices

- CommunityToolkit.Mvvm is the primary MVVM library.
- View models commonly inherit `ObservableObject` or `ObservableRecipient`.
- Generated properties use `[ObservableProperty]` on backing fields, with partial `On<Property>Changed` hooks for derived state and validation.
- Commands are commonly generated with `[RelayCommand]`; async commands return `Task`.
- Cross-view-model coordination uses `IMessenger` or `WeakReferenceMessenger.Default` with typed messages.
- Constructor injection is preferred for service dependencies; optional parameters are sometimes used in view models to make tests supply substitutes or simple default services.
- Code-behind remains present for WPF-specific behavior: window lifecycle, tray integration, keyboard/mouse handling, animations, layout helpers, popup handoff, and control-specific interaction.
- View models expose `ObservableCollection<T>` for bound collections and call `OnPropertyChanged` for computed properties such as counts, selection state, and `Can*` properties.

## Dependency Injection

- DI registration is extension-method based:
  - `Fluxo.Data.Extensions.AddFluxoData()`
  - `Fluxo.Extensions.AddFluxoPresentation()`
  - `Fluxo.Extensions.AddUIData()`
- `App` builds a `ServiceCollection`, chains the registration methods, and resolves the root application services.
- EF `FluxoDbContext`, repositories, and `IUnitOfWork` are scoped.
- `IDataOperationScopeFactory` and `IDataOperationRunner` are singletons that create scoped operation boundaries.
- UI shell view models such as `MainVM` and dashboard panel VMs are mostly singletons; popup and wizard VMs/views are transient.
- AutoMapper is configured once and registered as a singleton mapper with `EntityDtoProfile` and `DtoViewModelProfile`.
- `IMessenger` is registered as singleton `WeakReferenceMessenger.Default`.

## Async and Data Operations

- Data access methods are async and accept `CancellationToken` where exposed by repository/service contracts.
- Cross-repository mutations should run through `IDataOperationRunner.RunAsync(...)` so each operation gets a scoped `IUnitOfWork`, shared scoped repositories, centralized logging, and `DataOperationException` wrapping.
- `OperationCanceledException` and existing `DataOperationException` are allowed to pass through the runner unchanged.
- Persistence workflows commonly mutate entities, call repository update/add methods, then call `SaveChangesAsync`.
- View models use `IsBusy`/`IsSaving` flags to prevent duplicate submissions and expose command state.
- Longer reload/synchronization paths use `SemaphoreSlim` gates to avoid overlapping data refreshes.
- UI refresh notifications are sent with typed messenger contracts, for example `DashboardDataInvalidatedMessage`.

## XAML and Resources

- Shared resources are centralized in `Fluxo.Resources/Resources/**`.
- `Theme.xaml` defines named `Color.*` resources and matching `Brush.*` resources.
- Styles are split by concern under `Fluxo.Resources/Resources/Styles/` (`ButtonStyles.xaml`, `GlobalStyles.xaml`, `PopupStyles.xaml`, `MainWindowStyles.xaml`, etc.).
- Shared icons are resource paths in `Icons.xaml`; UI uses the shared `components:Icon` control instead of duplicating path markup.
- Fonts are bundled under `Fluxo.Resources/Resources/Fonts` and referenced by resource keys such as `Regular`, `Medium`, and `Bold`.
- Views bind to view model properties and commands; layout-specific templates and small local styles may live in the view XAML when narrowly scoped.
- `StaticResource` is common for stable resources; `DynamicResource` appears where runtime theme/resource updates are useful.
- Custom controls and shared controls live in `Fluxo.Resources/CustomControls` and `Fluxo.Resources/Components`; app-specific views live under `Fluxo/Views`.

## Migrations and Persistence Model

- EF Core SQLite is used with migrations stored in the `Fluxo` project under `Fluxo/Migrations`.
- Runtime and design-time configuration both use `FluxoDbContextFactory.BuildConnectionString()` and set the migrations assembly to `Fluxo`.
- Model configuration is centralized in `Fluxo.Data/Context/FluxoDbContext.cs` using private `Configure*` methods.
- Decimal money columns are explicitly configured as `NUMERIC`; some rates use `REAL`.
- Relationships generally use `DeleteBehavior.Restrict`.
- Reference navigations are configured for `AutoInclude`.
- Startup migration logic in `App.xaml.cs` handles normal `MigrateAsync`, legacy databases without `__EFMigrationsHistory`, history-table seeding, and migration inference from existing tables/columns.
- New schema changes should update the entity, `FluxoDbContext` model configuration, add an EF migration in `Fluxo/Migrations`, and consider legacy inference/upgrade behavior if the change affects existing user databases.

## Practical Risks

- Some view models and `App.xaml.cs` carry substantial orchestration responsibilities; prefer small extracted helpers for new complex logic.
- Tests include source/XAML string assertions for layout and code-behind behavior, so refactors can break tests even when runtime behavior is unchanged.
- Without a committed formatter config, keep edits visually consistent with neighboring files.

---

*Convention analysis refreshed: 2026-05-09*
