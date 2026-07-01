# Codebase Map: Structure

Generated: 2026-07-01

## Summary

Repository is a multi-project .NET 10 Windows solution. Source is split by domain, persistence, reusable services, shared WPF resources, desktop presentation, installer/packaging, and one cross-project xUnit test assembly.

## Repository Root

- `Fluxo.slnx` - solution definition for app, libraries, installer, WiX packages, and tests.
- `README.md` - user-facing overview, installation, and product documentation.
- `Dockerfile` - auxiliary container definition; not part of normal Windows WPF execution.
- `.github/workflows/build-on-final-commit.yml` - build/release automation.
- `docs/images/` - README/product screenshots and branding assets.
- `.planning/codebase/` - local generated codebase map.
- `artifacts/`, project `bin/`, `obj/`, and project `artifacts/` directories - generated output, not source boundaries.

## `Fluxo.Core/` - Domain and Contracts

- `Entities/` - persisted `Account`, `Transaction`, `Tag`, `SavingGoal`, `RecurringTransaction`, `Notification`, `UserSettings`, and `BudgetAllocation` models.
- `DTO/` - application transfer shapes for accounts, transactions, tags, goals, recurrence, calendar, analytics, and settings.
- `Enums/` - transaction/account types, budget policies, UI view modes, data-management decisions, and settings actions.
- `Interfaces/Repositories/` - generic read/write contracts plus entity-specific repositories.
- `Interfaces/Services/` - app-facing service contracts for persistence, analytics, calendar, backup, logging, startup, and UI-lock protection.
- `Interfaces/Operations/` - scoped data-operation runner, factory, scope, and popup-host contracts.
- `Interfaces/History/` - reversible in-memory log action contract.
- `Budgeting/` - allocation calculator, balancer, period rules, state, and snapshots.
- `Constants/` - system tag names and user-setting names/value parsing.
- `Filters/` - repository query inputs for accounts and transactions.
- `Exceptions/DataOperationException.cs` - public operation failure type.

## `Fluxo.Data/` - SQLite Persistence

- `Context/FluxoDbContext.cs` - EF Core model configuration and save-time normalization.
- `Context/FluxoDbContextFactory.cs` - runtime/design-time SQLite path and connection construction.
- `Repositories/Repository.cs` - generic repository base.
- `Repositories/*Repository.cs` - account, transaction, tag, goal, recurrence, notification, settings, and budget queries.
- `Operations/` - per-operation async DI scope factory, wrapper, and centralized error translation.
- `UnitOfWork.cs` - typed repository aggregation plus `SaveChangesAsync`.
- `Extensions/ServiceCollectionExtensions.cs` - EF Core, repository, unit-of-work, and operation DI registrations.
- `Properties/AssemblyInfo.cs` - internal visibility metadata used by tests.

## `Fluxo.Services/` - Reusable Application Services

- `Persistence/` - account, transaction, tag, analytics, calendar, app-data, and budget-period synchronization services.
- `Backups/FluxoUserBackupDocument.cs` - serialized backup document contract.
- `Backups/UserBackupService.cs` - export, append/overwrite, restore, and restore-safety logic.
- `Logging/FluxoLogService.cs` - service wrapper over application logging.
- `Mappings/EntityDtoProfile.cs` - AutoMapper entity-to-DTO definitions.
- `Ui/DpapiUiLockPasswordProtector.cs` - Windows DPAPI-based UI-lock secret protection.

## `Fluxo.Resources/` - Shared WPF Layer

- `Resources/Theme.xaml`, `Fonts.xaml`, `Icons.xaml`, `Converters.xaml` - merged resource dictionaries.
- `Resources/Styles/` - global, window, popup, settings, wizard, input, button, container, and navigator styles.
- `Resources/Fonts/` and `Resources/fluxo.ico` - embedded visual assets.
- `Resources/Messages/` - CommunityToolkit messenger payload types shared across presentation features.
- `CustomControls/` - popup base, money/numeric/suffix inputs, balloon controls, segmented toggles, step navigator, swipe reveal, and layout helpers.
- `Components/` - reusable account, analytics chart, date selector, expense list, icon, message box, wave, and background components.
- `Converters/` - money, numeric, date, brush/color, visibility, geometry, and label converters.
- `Behaviors/` and `Infrastructure/` - attached scrolling behavior, dependency-object tree traversal, and window invocation helpers.

## `Fluxo/` - Desktop Executable

- `App.xaml(.cs)` - process composition, startup pipeline, migration compatibility, tray lifecycle, backups, updates, and global failure handling.
- `Extensions/ServiceCollectionExtensions.cs` - app services, view models, pages, popups, and window registrations.
- `Mappings/DtoViewModelProfile.cs` - DTO-to-WPF model mappings.
- `Infrastructure/AssemblyResolutionBootstrap.cs` - resolves assemblies from organized output folders.
- `Infrastructure/SingleInstance/` - mutex/named-pipe coordination and startup policy.
- `Migrations/` - timestamped EF Core migrations, designer metadata, snapshot, and design-time context factory.
- `Services/Dialogs/` - WPF dialog abstraction and implementation.
- `Services/History/` - reversible action types and in-memory history manager.
- `Services/Notifications/` - notification grouping, action execution, and startup summaries.
- `Services/Transactions/` - repayment transaction support.
- `Services/Ui/` - startup registration, tray popup policy, and UI-settle awaiting.
- `Services/Updates/` - version resolution, update checking, notification, interaction, installer launching, and lifecycle coordination.
- `Helper/MainWindow/` - pure helpers for search, shortcuts, drag/restore bounds, detail targeting, and animation bounds.
- `Helper/Settings/` - setup-wizard and update-check settings flows.

## `Fluxo/ViewModels/` - Presentation State

- `Entities/` - bindable account, transaction, tag, goal, recurrence, notification, and settings models.
- `Shell/Main/` - `MainVM`, dashboard/page models, allocation/spending panels, ledger filters/export/grouping, calendar, notifications, goals, and upcoming events.
- `Shell/QuickSetupWizard/` - wizard coordinator, step models, draft records, loading outcome, and summary/final state.
- `Shell/AutoLockPreset.cs` - shell lock preset values.
- `Popups/` - transaction/account/tag/goal editors, forecast, reconciliation, notification actions, transfers, and detail models.
- `Popups/Settings/` - settings orchestrator and separate accounts, budget, debts/IOUs, goals, recurrence, tags, and personalization tabs.
- `Popups/DataManagement/` - import/export/restore modes, entity choices, conflicts, and operation state.
- `Popups/Planning/PlanningReportVM.cs` - planning report popup state; unrelated to `.planning/` project-management files.
- `Controls/` and `CustomControls/` - small view-model types used by reusable controls.

## `Fluxo/Views/` - WPF Views

- `Shell/Main/MainWindow.xaml(.cs)` - root window, navigation, keyboard/mouse interaction, page scopes, popups, tray hide/restore, and header search.
- `Shell/Main/Pages/` - dashboard, analytics, calendar, and ledger pages.
- `Shell/Main/Sections/` - allocation, budget, spending, notifications, saving goals, and upcoming-event dashboard sections.
- `Shell/Main/Controls/` - day spinner and main-view-mode controls.
- `Shell/Wizard/` - quick-setup host, shell pages, and step views.
- `Shell/Tray/` - tray menu and startup notification windows.
- `Shell/StartupLoaderPopup.xaml(.cs)` - startup-stage loading surface.
- `Popups/` - feature popup XAML/code-behind pairs.
- `Popups/Settings/Tabs/` - settings tab views matching settings tab view models.

## Installer and Packaging Projects

- `Fluxo.Installer/Program.cs` - global mutex and WiX managed bootstrapper launch.
- `Fluxo.Installer/BootstrapperEntry.cs` - Burn callbacks, interactive/headless paths, elevation, planning, apply, and exit codes.
- `Fluxo.Installer/Models/` - installer mode, requested operation, screen, checklist, maintenance, elevation, and update decisions.
- `Fluxo.Installer/Services/` - .NET runtime detection/installation/ownership, installed-version lookup, release resolution, and legacy cleanup.
- `Fluxo.Installer/ViewModels/InstallerViewModel.cs` - installer workflow state machine.
- `Fluxo.Installer/Views/Pages/` - welcome, app-found, progress, finished, and uninstall pages.
- `Fluxo.Installer.Msi/Package.wxs` - MSI product/package authoring.
- `Fluxo.Installer.Msi/Folders.wxs`, `ExampleComponents.wxs`, `Package.en-us.wxl` - install layout, components, and localization.
- `Fluxo.Installer.Bundle/Bundle.wxs` - Burn bundle chain and managed bootstrapper payload.

## `Fluxo.Tests/` - Verification

- `Budgeting/` - allocation calculator and balancer tests.
- `Extensions/` - DI registration tests.
- `Infrastructure/` - migrations/schema, database paths, operation runner, assembly loading, lifetime safety, repositories, and single-instance policy.
- `Installer/` - runtime, registry, elevation, modes, state, MSI authoring, launch, and cleanup behavior.
- `Packaging/` - executable/output naming contracts.
- `Services/` - backups, dialogs, history, logging, notifications, persistence, transactions, UI, and updates.
- `ViewModels/` - popup, settings, shell, dashboard panel, ledger, wizard, and custom-control behavior.
- `Views/` - XAML/layout/resource/style assertions for controls, popups, shell, tray, and wizard.
- `TestDoubles/InlineDataOperationRunner.cs` - minimal operation-runner test double.
- `TestSupport/` - repository path and Windows path fixtures.

## Naming and Placement Rules

- View models end in `VM`; entity-facing bindable models sit in `ViewModels/Entities/`.
- WPF view code-behind mirrors XAML names as `.xaml.cs`; feature folders pair view and view-model namespaces.
- Interfaces use `I...`; repository/service implementations normally drop the `I` prefix and retain `Repository`/`Service` suffixes.
- Messenger event types end in `Message` and live centrally in `Fluxo.Resources/Resources/Messages/`.
- EF migration files use timestamp prefixes and remain under `Fluxo/Migrations/` because the executable is the migrations assembly.
- Pure decision/format/resolve helpers use descriptive suffixes such as `Policy`, `Resolver`, `Formatter`, `Matcher`, `Flow`, or `Support` and sit near their consumer.
- Build outputs and local planning artifacts are not runtime source and must not be treated as project boundaries.
