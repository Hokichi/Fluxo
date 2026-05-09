# Technology Stack

**Analysis Date:** 2026-05-09

## App Type

- Windows desktop finance app built with WPF.
- Local-first architecture: UI, domain models, services, and SQLite persistence run in-process.
- Installer stack includes a WiX MSI package, WiX bundle, and custom managed bootstrapper UI.
- No hosted backend, web frontend, or app-side Node runtime detected.

## Languages and UI Markup

- C# for application, services, data access, installer, and tests.
- XAML for WPF windows, pages, popups, controls, styles, themes, and installer UI.
- WiX XML (`*.wxs`, `*.wixproj`) for MSI and bundle authoring.
- YAML for GitHub Actions release workflow.
- JSON is minimal; root `package-lock.json` has no packages and does not represent a Node toolchain.

## Runtime Targets

- All .NET projects target `net10.0-windows`.
- Main app and managed installer target `win-x64`.
- Main app output type is `WinExe`; installer bootstrapper is also `WinExe`.
- WPF is enabled across production projects, including shared resource/core projects.
- Main app references `Microsoft.WindowsDesktop.App.WindowsForms` for tray icon integration.
- No `global.json` detected, so the .NET SDK version is not pinned in the repo.

## Solution and Projects

- Solution: `Fluxo.slnx`.
- `Fluxo`: WPF app shell, startup lifecycle, tray behavior, views, view models, mappings.
- `Fluxo.Core`: entities, DTOs, enums, filters, interfaces, shared constants/exceptions.
- `Fluxo.Data`: EF Core `DbContext`, repositories, unit of work, scoped data operation runner.
- `Fluxo.Services`: persistence/application services, dialogs, notifications, logging.
- `Fluxo.Resources`: shared WPF resources, custom controls, converters, fonts, icons, message contracts.
- `Fluxo.Installer`: custom WPF/WiX managed bootstrapper application.
- `Fluxo.Installer.Msi`: WiX MSI package authoring.
- `Fluxo.Installer.Bundle`: WiX bundle authoring for the installer executable.
- `Fluxo.Tests`: xUnit tests for services, infrastructure, installer behavior, view models, and UI logic.

## Frameworks and Key Libraries

- WPF for desktop UI composition.
- Entity Framework Core 10 with SQLite provider for local persistence.
- CommunityToolkit.Mvvm for observable objects, commands, and weak-reference messaging.
- Microsoft.Extensions.DependencyInjection for manual DI composition via `ServiceCollection`.
- AutoMapper for entity/DTO/view-model mappings.
- Serilog with file sink for local file logging.
- FluentValidation is referenced by `Fluxo.Services`.
- MahApps icon packs are not currently referenced; icon assets are local WPF resources.
- Microsoft.Toolkit.Uwp.Notifications is referenced but no active toast API usage was found in source.
- WiX Toolset 7 for MSI/bundle packaging and managed bootstrapper integration.

## Build, Test, and Release Tooling

- Primary local commands: `dotnet restore`, `dotnet build`, `dotnet test`.
- WiX installer build target: `dotnet build .\Fluxo.Installer.Bundle\Fluxo.Installer.Bundle.wixproj --configuration Release`.
- MSI project builds the main Fluxo app before packaging.
- Main app build/publish reorganizes output into `libs/` and `vendor/`, creates a hard link for the root executable after build, and removes PDBs after build.
- Dockerfile publishes a self-contained, single-file `win-x64` app using Windows container images.
- GitHub Actions workflow `.github/workflows/build-on-final-commit.yml` builds and publishes an installer only when the push commit message matches `Final commit for build vX.Y.Z`.
- Tests use xUnit, `Microsoft.NET.Test.Sdk`, and NSubstitute.

## Persistence and Migrations

- EF Core migrations are in `Fluxo/Migrations/**` and use the `Fluxo` assembly as the migrations assembly.
- Runtime startup calls `MigrateAsync` and includes fallback logic for legacy/local databases with missing EF migration history.
- Data access is repository/unit-of-work based and typically flows through `IDataOperationRunner`.

## Runtime Assumptions

- Development requires Windows and .NET 10 SDK.
- Runtime is Windows desktop; tray, registry, named pipe, mutex, and WiX paths are Windows-specific.
- Primary app data file is `fluxo.db` under `AppContext.BaseDirectory`.
- Logs are written under `AppContext.BaseDirectory\logs`.
- Default installer target is `C:\Program Files\fluxo`.

## Inventory Snapshot

- `*.cs`: 412
- `*.xaml`: 78
- `*.wxs`: 4
- `*.wixproj`: 2

---

*Stack analysis refreshed: 2026-05-09*
