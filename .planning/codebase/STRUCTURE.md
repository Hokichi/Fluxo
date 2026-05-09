# Codebase Structure

**Analysis Date:** 2026-05-09

## Top-Level Layout

```text
Fluxo/
|-- .github/                    # GitHub automation/configuration
|-- .nuget/                     # Local NuGet configuration/assets
|-- .planning/                  # GSD planning and codebase map documents
|-- artifacts/                  # Local verification/build artifacts
|-- docs/                       # Project documentation
|-- Fluxo/                      # Main WPF desktop app
|-- Fluxo.Core/                 # Domain models and contracts
|-- Fluxo.Data/                 # EF Core SQLite persistence
|-- Fluxo.Services/             # Application services, mappings, logging
|-- Fluxo.Resources/            # Shared WPF resources and controls
|-- Fluxo.Installer/            # Managed WiX bootstrapper WPF app
|-- Fluxo.Installer.Msi/        # WiX MSI authoring
|-- Fluxo.Installer.Bundle/     # WiX Burn bundle authoring
|-- Fluxo.Tests/                # xUnit tests
|-- Dockerfile
|-- DESIGN.md
`-- Fluxo.slnx                  # Solution manifest
```

Ignored/generated areas:

- `bin/` and `obj/` under project folders.
- Root `obj/`.
- `.worktree/` and other local worktree/tooling folders.
- Local binary logs such as `msbuild.binlog` and `fluxo-tests.binlog`.

## Solution Projects

`Fluxo.slnx` includes:

- `Fluxo/Fluxo.csproj` - main `net10.0-windows` WPF `WinExe`, `win-x64`.
- `Fluxo.Core/Fluxo.Core.csproj` - shared domain/contracts library.
- `Fluxo.Data/Fluxo.Data.csproj` - EF Core SQLite data access.
- `Fluxo.Services/Fluxo.Services.csproj` - application services and logging.
- `Fluxo.Resources/Fluxo.Resources.csproj` - WPF resources, controls, converters, messages.
- `Fluxo.Installer/Fluxo.Installer.csproj` - managed WPF bootstrapper app.
- `Fluxo.Installer.Msi/Fluxo.Installer.Msi.wixproj` - WiX MSI package.
- `Fluxo.Installer.Bundle/Fluxo.Installer.Bundle.wixproj` - WiX bundle.
- `Fluxo.Tests/Fluxo.Tests.csproj` - xUnit/NSubstitute test project.

All C# projects target `net10.0-windows`; WPF is enabled broadly across production projects.

## Main App: `Fluxo/`

Purpose: app startup, DI composition, runtime UI, shell workflows, migrations, and app-local UI services.

Important files:

- `App.xaml` / `App.xaml.cs` - WPF application entry point, DI setup, startup migration, single-instance handling, tray lifetime, first-run wizard, main-window activation.
- `AssemblyInfo.cs` - WPF theme metadata.
- `GlobalUsings.Resources.cs` - resource/global using support.
- `Fluxo.csproj` - app target, package references, project references, output organization targets.
- `Extensions/ServiceCollectionExtensions.cs` - registers AutoMapper, application services, UI services, view models, popups, and main window.
- `Mappings/DtoViewModelProfile.cs` - DTO-to-view-model mapping.
- `Migrations/` - EF Core migrations and design-time context factory.
- `Infrastructure/SingleInstance/` - single-instance coordinator and startup policy.
- `Services/Dialogs/` - dialog service abstraction and implementation.
- `Services/Notifications/` - notification action/grouping/startup summary logic.
- `Services/Ui/` - UI settle waiting and startup registration.
- `Services/History/` - log memory manager/actions.

UI layout:

- `Views/Shell/Main/` - `MainWindow`, shell helpers, shortcuts, restore-bounds interpolation, drag state, search helper.
- `Views/Shell/Main/Sections/` - dashboard panels.
- `Views/Shell/Main/Controls/` - dashboard controls such as day spinner and view-mode toggle.
- `Views/Shell/Tray/` - tray menu and startup notification popup.
- `Views/Shell/Wizard/` - quick setup wizard shell and pages.
- `Views/Shell/Wizard/Pages/Steps/` - setup wizard step pages.
- `Views/Popups/` - transaction, source, goal, tag, settings, planning, analytics, delete, toast, transfer, and placeholder popups.
- `Views/Popups/Settings/Tabs/` - settings tab views.
- `Views/Behaviors/` - app-local view behaviors.

View-model layout:

- `ViewModels/Shell/Main/` - dashboard and shell panel view models.
- `ViewModels/Shell/QuickSetupWizard/` - first-run/setup wizard view models and draft models.
- `ViewModels/Popups/` - popup workflow view models.
- `ViewModels/Popups/Settings/` - settings root/tab/item view models and shared helpers.
- `ViewModels/Popups/Planning/` - planning popup/report view models.
- `ViewModels/Popups/Helpers/` - UI helper factories and date helpers.
- `ViewModels/Entities/` - bindable entity view models.
- `ViewModels/Controls/` and `ViewModels/CustomControls/` - control-specific view models.

## Domain: `Fluxo.Core/`

Purpose: stable contracts and domain vocabulary.

Key folders:

- `Entities/` - EF/domain entity classes.
- `DTO/` - service transfer objects.
- `Enums/` - app/domain enums.
- `Filters/` - query filter types.
- `Constants/` - user setting names and parsing helpers.
- `Exceptions/` - domain/application exception types.
- `Interfaces/Repositories/` - read/write/generic and entity-specific repository interfaces.
- `Interfaces/Services/` - application service interfaces.
- `Interfaces/Operations/` - data operation runner/scope contracts and popup host abstraction.
- `Interfaces/History/` - history/log memory action contract.
- `Interfaces/IUnitOfWork.cs` - repository aggregation and commit contract.

## Persistence: `Fluxo.Data/`

Purpose: implementation of persistence contracts.

Key files/folders:

- `Context/FluxoDbContext.cs` - EF model configuration.
- `Context/FluxoDbContextFactory.cs` - runtime/design-time SQLite connection factory.
- `Repositories/Repository.cs` - generic repository base.
- `Repositories/*Repository.cs` - entity-specific query/write implementations.
- `UnitOfWork.cs` - repository aggregation and `SaveChangesAsync`.
- `Operations/DataOperationScope.cs` - async DI scope wrapper.
- `Operations/DataOperationScopeFactory.cs` - creates scoped data operation objects.
- `Operations/DataOperationRunner.cs` - operation execution, logging, exception wrapping.
- `Extensions/ServiceCollectionExtensions.cs` - `AddFluxoData()` registrations.

## Services: `Fluxo.Services/`

Purpose: application use cases and cross-cutting service implementations.

Key folders:

- `Persistence/` - `ExpenseService`, `ExpenseLogService`, `SpendingSourceService`, `TagService`, `AnalyticsService`, `AppDataService`.
- `Mappings/EntityDtoProfile.cs` - entity-to-DTO AutoMapper profile.
- `Logging/FluxoLogService.cs` - `ILogService` implementation.

Notable dependencies:

- AutoMapper for entity/DTO mapping.
- FluentValidation package is referenced, though validators are not organized as a visible top-level folder in the current tree.
- Serilog and file sink support logging.
- Microsoft Toolkit UWP Notifications supports Windows notification workflows.

## Shared Resources: `Fluxo.Resources/`

Purpose: reusable WPF visual infrastructure and UI message contracts.

Key folders:

- `Resources/Theme.xaml` - shared theme dictionary.
- `Resources/Fonts.xaml` and `Resources/Fonts/*.ttf` - bundled font resources.
- `Resources/Icons.xaml` and `Resources/fluxo.ico` - icon resources.
- `Resources/Converters.xaml` - converter resource dictionary.
- `Resources/Styles/` - button, container, global, main window, popup, quick setup wizard, settings, step navigator, and text box styles.
- `Resources/Messages/` - CommunityToolkit messenger message types for dashboard, settings, wizard, date range, view mode, history, and username changes.
- `Converters/` - WPF value converters and money format utilities.
- `CustomControls/` - reusable controls such as popup base, message box, money text box, step navigator, swipe reveal, balloon controls, and fading scroll viewer.
- `Components/` - reusable XAML components such as analytics chart, date selector, expenses list, income source, icon, wave/background, and message box popup.
- `Behaviors/` and `Infrastructure/` - shared WPF behavior/invocation helpers.

## Installer Projects

### `Fluxo.Installer/`

Purpose: managed WPF Burn bootstrapper UI.

Key files/folders:

- `Program.cs` - single-instance mutex and Burn app entry point.
- `BootstrapperEntry.cs` - `InstallerBootstrapperApplication`, Burn event handling, interactive/headless mode, related bundle detection, up-to-date handling, plan/apply dispatch.
- `App.xaml` / `App.xaml.cs` - installer WPF app resource setup.
- `ViewModels/InstallerViewModel.cs` - installer workflow state machine, commands, prerequisites, rollback, repair/uninstall/install, running app termination, repairer/deferred cleanup handling.
- `Models/` - installer state, screen, operation mode, request type, maintenance action, checklist step state, up-to-date decision.
- `Services/` - .NET runtime detector and installed-version registry reader.
- `Views/MainWindow.xaml` - installer shell.
- `Views/Pages/` - welcome, app found, progress, finished, and uninstall pages.
- `fluxo.ico` - installer icon/content.

### `Fluxo.Installer.Msi/`

Purpose: WiX MSI authoring for installed app files.

Key files:

- `Fluxo.Installer.Msi.wixproj` - WiX package project; builds the main `Fluxo` application before MSI build and defines `FluxoAppOutputDir`.
- `Package.wxs` - package identity, major upgrade, main feature, install sequence.
- `Folders.wxs` - folder layout.
- `ExampleComponents.wxs` - component group authoring for app files.
- `Package.en-us.wxl` - localization strings.

### `Fluxo.Installer.Bundle/`

Purpose: WiX Burn bundle authoring.

Key files:

- `Fluxo.Installer.Bundle.wixproj` - bundle project; references managed bootstrapper and MSI projects.
- `Bundle.wxs` - bundle identity, managed bootstrapper payloads, `InstallFolder` variable, and chained MSI package.

## Tests: `Fluxo.Tests/`

Purpose: unit and focused integration-style tests using xUnit and NSubstitute.

Test layout mirrors production areas:

- `Extensions/` - service registration tests.
- `Infrastructure/` - data operation runner, DI registration, lifetime safety, single-instance policy.
- `Installer/` - bootstrapper/model/service/WiX authoring behavior.
- `Services/Dialogs/` - dialog service.
- `Services/Notifications/` - notification grouping/actions/startup summaries.
- `Services/Persistence/` - persistence service behavior.
- `Services/Ui/` - tray popup display policy.
- `TestDoubles/` - test operation runner and other doubles.
- `ViewModels/CustomControls/` - control view-model behavior.
- `ViewModels/Popups/` - popup workflow view models.
- `ViewModels/Popups/Planning/` - planning popup/report view models.
- `ViewModels/Popups/Settings/` - settings tab/root orchestration behavior.
- `ViewModels/Shell/Main/` - dashboard panel and shell helper view models.
- `ViewModels/Shell/StartupWizard/` - startup wizard data/coordinator behavior.
- `Views/Components/`, `Views/CustomControls/`, `Views/Popups/`, `Views/Shell/Main/`, `Views/Shell/Tray/`, `Views/Styles/` - layout, style, keyboard, popup handoff, tray, and window helper tests.

## Naming and Organization Conventions

- Production C# types use `PascalCase` file names matching type names.
- View models generally end in `VM`.
- Tests generally end in `Tests`.
- WPF views use paired `.xaml` and `.xaml.cs` files.
- Service implementations end in `Service`.
- Repository implementations end in `Repository`.
- Messenger payloads end in `Message`.
- Enum-like installer state types are under `Fluxo.Installer/Models`.
- EF migrations use timestamp-prefixed names under `Fluxo/Migrations`.
- Partial view models are used where a model has a focused split, such as `SavingGoalVM.Progress.cs`.

## Migration Organization

EF migrations live in the main app project:

- `Fluxo/Migrations/*.cs`
- `Fluxo/Migrations/FluxoDbContextModelSnapshot.cs`
- `Fluxo/Migrations/FluxoDesignTimeDbContextFactory.cs`

Runtime data registration in `Fluxo.Data` points SQLite migrations to the `Fluxo` assembly. Future schema changes should keep migrations in this location unless the migration assembly configuration is changed.

Current migration history includes changes for spending source UI flags, user settings, soft-delete flags, monthly due dates, system tags, notifications/deduct source, numeric money columns, expense tag icon removal, and saving goal creation date.

## Resource Organization

Reusable WPF assets are concentrated in `Fluxo.Resources`:

- High-level dictionaries in `Resources/*.xaml`.
- Style dictionaries in `Resources/Styles/*.xaml`.
- Font binaries in `Resources/Fonts/*.ttf`.
- Value converters in `Converters/*.cs` with aggregate registration through `Resources/Converters.xaml`.
- Message payloads in `Resources/Messages/*.cs`.
- Reusable controls/components in `CustomControls/` and `Components/`.

App-specific views and behaviors remain under `Fluxo/Views`; shared visual primitives belong under `Fluxo.Resources`.

## Where to Add New Code

- Domain entity/DTO/enum/filter/contract: `Fluxo.Core`.
- EF model, repository, unit-of-work, operation-scope code: `Fluxo.Data`.
- DTO-oriented use case or persistence orchestration: `Fluxo.Services/Persistence`.
- Logging/mapping service support: `Fluxo.Services`.
- App startup, DI composition, shell services, notifications, dialogs, single-instance, tray behavior: `Fluxo`.
- Main dashboard UI: `Fluxo/Views/Shell/Main` and `Fluxo/ViewModels/Shell/Main`.
- First-run setup UI: `Fluxo/Views/Shell/Wizard` and `Fluxo/ViewModels/Shell/QuickSetupWizard`.
- Popup workflow UI: `Fluxo/Views/Popups` and `Fluxo/ViewModels/Popups`.
- Shared reusable WPF controls/styles/converters/messages: `Fluxo.Resources`.
- Installer UI logic: `Fluxo.Installer`.
- MSI authoring: `Fluxo.Installer.Msi`.
- Bundle/Bootstrapper authoring: `Fluxo.Installer.Bundle`.
- Tests: mirror the production area under `Fluxo.Tests`.

---

*Structure analysis refreshed: 2026-05-09*
