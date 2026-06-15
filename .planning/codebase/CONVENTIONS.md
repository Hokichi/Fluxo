# Codebase Map: Conventions

Generated: 2026-06-15

## Summary

Fluxo follows conventional C#/.NET naming, WPF MVVM patterns, dependency injection, scoped data operations, and xUnit-style unit tests. Code tends to favor explicit classes and narrowly named services over generic utility layers.

## C# Style

- File-scoped namespaces are used in most C# files.
- Nullable reference types are enabled across projects.
- Implicit usings are enabled.
- Primary constructors are common in services and repositories, for example `Fluxo.Data/UnitOfWork.cs`.
- Async methods are named with `Async`.
- Cancellation tokens appear on repository and service APIs where I/O or EF work occurs.
- Guard clauses use `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrWhiteSpace`.

## MVVM Style

- CommunityToolkit.Mvvm is used for observable properties and recipients.
- View models use names such as `MainVM`, `DashboardVM`, `SettingsVM`, and `QuickAddVM`.
- Messenger events use `WeakReferenceMessenger` and message records in `Fluxo.Resources/Resources/Messages/`.
- Views are generally concrete WPF windows/pages/popups under `Fluxo/Views`.
- View models are registered through DI in `Fluxo/Extensions/ServiceCollectionExtensions.cs`.

## Dependency Injection

- `IServiceCollection` extension methods hold registration logic.
- `AddFluxoData` registers EF Core, repositories, unit of work, and data operation services.
- `AddFluxoPresentation` registers persistence/business services and AutoMapper.
- `AddUIData` registers UI services, view models, pages, popups, and `MainWindow`.
- Most repositories and data services are scoped or transient; shell view models and long-lived UI services are often singleton.

## Data Access

- Code should use `IDataOperationRunner` for app data access when crossing UI/service boundaries.
- Data operations receive an `IDataOperationScope` and use `scope.UnitOfWork`.
- Repositories expose async reads and synchronous update/remove state changes.
- `SaveChangesAsync` is explicit through `IUnitOfWork`.
- `AsNoTracking` and `AsNoTrackingWithIdentityResolution` are preferred for query results.
- Soft deletion appears for some logs and notifications through `IsForDeletion` or `IsCleared`.

## Mapping

- Entity-to-DTO mappings are in `Fluxo.Services/Mappings/EntityDtoProfile.cs`.
- DTO-to-view-model mappings are in `Fluxo/Mappings/DtoViewModelProfile.cs`.
- `SpendingSource` has special mapping rules for computed fields and entity ID handling.

## Error Handling

- Data operation failures are logged and wrapped in `DataOperationException`.
- UI flows commonly show dialogs or toast popups through `IDialogService`.
- Startup and global exception handlers log via `FluxoLogManager`.
- Installer flow stores operation state in `InstallerState` and uses explicit operation modes.

## WPF Resources

- Shared styles live under `Fluxo.Resources/Resources/Styles/`.
- Converters live under `Fluxo.Resources/Converters/`.
- Reusable controls live under `Fluxo.Resources/CustomControls/`.
- XAML resource dictionaries are central to UI styling rather than inline-only styling.

## Tests

- xUnit facts/theories are the primary test style.
- NSubstitute is used for mocks.
- Test namespaces mirror functional areas: `Infrastructure`, `Installer`, `Services`, `ViewModels`, `Views`.
- UI logic is heavily tested through view model and layout/helper classes rather than end-to-end UI automation.

## Release Conventions

- Normal pushes do not build release installers unless commit message matches `Final commit for build vX.Y.Z`.
- Release version flows into WiX bundle output through `FluxoInstallerVersion`.
- Installer output name convention is `fluxo-X.Y.Z-Installer.exe`.

## Local Tooling Conventions

- `.gitignore` ignores `.codex/`, `.claude/`, `.superpowers/*`, `docs/*.md`, build outputs, logs, binlogs, and SQLite databases.
- `.planning/codebase` is not ignored by current rules.
