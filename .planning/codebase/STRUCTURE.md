# Codebase Map: Structure

Generated: 2026-06-15

## Summary

The repository is organized as a multi-project .NET solution. Project boundaries mostly separate domain contracts, persistence, services, UI resources, application UI, installer, packaging, and tests.

## Root

- `Fluxo.slnx` - solution definition.
- `README.md` - user-facing product documentation.
- `Dockerfile` - present, not part of primary Windows desktop build flow.
- `.github/workflows/build-on-final-commit.yml` - release workflow.
- `.gitignore` - Visual Studio/.NET ignores plus local tool folders and generated docs.
- `.planning/codebase/` - generated GSD codebase map.

## Main App Project

- `Fluxo/App.xaml` and `Fluxo/App.xaml.cs` - application startup, migration, tray, backup, update shutdown.
- `Fluxo/Views/Shell/Main/` - main window, pages, sections, controls, shell helpers.
- `Fluxo/Views/Shell/Wizard/` - first-run quick setup wizard.
- `Fluxo/Views/Shell/Tray/` - tray menu and startup notification popup.
- `Fluxo/Views/Popups/` - transaction, settings, analytics, planning, data management, toast, and detail popups.
- `Fluxo/ViewModels/Shell/` - main shell and wizard view models.
- `Fluxo/ViewModels/Popups/` - popup view models.
- `Fluxo/ViewModels/Entities/` - WPF-facing entity view models.
- `Fluxo/Services/` - app-specific dialogs, history, notifications, UI, updates.
- `Fluxo/Migrations/` - EF Core migration files and model snapshot.
- `Fluxo/Infrastructure/SingleInstance/` - mutex and named-pipe single-instance coordination.

## Core Project

- `Fluxo.Core/Entities/` - persisted domain entities.
- `Fluxo.Core/DTO/` - transfer models.
- `Fluxo.Core/Enums/` - domain enum definitions.
- `Fluxo.Core/Interfaces/` - service, repository, operation, and history contracts.
- `Fluxo.Core/Constants/` - settings names, parsers, system tags.
- `Fluxo.Core/Budgeting/` - budget allocation period and calculator logic.
- `Fluxo.Core/Filters/` - query filter input models.

## Data Project

- `Fluxo.Data/Context/` - EF Core context and context factory.
- `Fluxo.Data/Repositories/` - generic and entity-specific repositories.
- `Fluxo.Data/Operations/` - scoped operation runner and scope implementation.
- `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs` - persistence DI setup.
- `Fluxo.Data/UnitOfWork.cs` - repository aggregation and save boundary.

## Services Project

- `Fluxo.Services/Persistence/` - business services over data operations.
- `Fluxo.Services/Backups/` - backup/export/restore support.
- `Fluxo.Services/Logging/` - logging service integration.
- `Fluxo.Services/Mappings/EntityDtoProfile.cs` - entity to DTO AutoMapper profile.

## Resources Project

- `Fluxo.Resources/Resources/*.xaml` - themes, fonts, icons, converters, and style dictionaries.
- `Fluxo.Resources/Resources/Styles/` - WPF style dictionaries.
- `Fluxo.Resources/CustomControls/` - reusable controls such as `BasePopup`, `MoneyTextBox`, `NumericUpDown`, segmented controls, and step navigator.
- `Fluxo.Resources/Components/` - shared UI components such as analytics chart, date selector, expenses list, and wave background.
- `Fluxo.Resources/Converters/` - WPF value converters.
- `Fluxo.Resources/Resources/Messages/` - messenger payload records.

## Installer Projects

- `Fluxo.Installer/` - managed bootstrapper WPF application.
- `Fluxo.Installer/Views/Pages/` - installer pages.
- `Fluxo.Installer/ViewModels/InstallerViewModel.cs` - installer state machine.
- `Fluxo.Installer/Services/` - runtime detection/installation, registry reading, cleanup helpers.
- `Fluxo.Installer.Msi/Package.wxs` - MSI package authoring.
- `Fluxo.Installer.Bundle/Bundle.wxs` - bundle authoring.

## Tests

- `Fluxo.Tests/Budgeting/` - budget calculator coverage.
- `Fluxo.Tests/Infrastructure/` - migrations, schema, lifetime safety, registration.
- `Fluxo.Tests/Installer/` - installer state, runtime, MSI, and launch behavior.
- `Fluxo.Tests/Packaging/` - executable naming.
- `Fluxo.Tests/Services/` - persistence, updates, backups, notifications, dialogs.
- `Fluxo.Tests/ViewModels/` - shell, popup, settings, wizard view models.
- `Fluxo.Tests/Views/` - UI layout, controls, styles, shell, popups, tray.
- `Fluxo.Tests/TestDoubles/` and `Fluxo.Tests/TestSupport/` - test helpers.

## Naming Conventions

- View models generally end with `VM`.
- WPF view code-behind files mirror XAML names with `.xaml.cs`.
- Service interfaces use `I...Service`.
- Repository interfaces use `I...Repository`.
- EF migrations use timestamp-prefixed filenames under `Fluxo/Migrations/`.
