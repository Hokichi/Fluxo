# Codebase Map: Stack

Generated: 2026-07-01
Branch: `main`

## Summary

Fluxo is a Windows x64 personal-finance desktop application built on .NET 10 and WPF. The repository contains the executable, domain, EF Core persistence, application services, reusable WPF resources, a managed WiX installer UI, MSI/bundle packaging, and xUnit tests.

## Languages And Runtime

- C# with nullable reference types and implicit usings enabled across all .NET projects.
- XAML for WPF windows, pages, controls, styles, templates, icons, and resources.
- WiX XML (`.wxs`) for MSI and bootstrapper bundle authoring.
- MSBuild XML (`.csproj`, `.wixproj`, `.slnx`) plus PowerShell/cmd targets for output layout and packaging.
- GitHub Actions YAML with Bash and PowerShell release steps.
- Windows-container Dockerfile using PowerShell escape syntax.
- Runtime target: `net10.0-windows`; current local SDK observed: `10.0.301`.
- Desktop framework: WPF; main app additionally references Windows Forms for `NotifyIcon`.
- Runtime identifier and installer platform: `win-x64` / x64.
- Nullable reference types: enabled. No explicit `LangVersion`; compiler default follows .NET 10 SDK.

## Solution And Projects

- `Fluxo.slnx` - solution entry point.
- `Fluxo/Fluxo.csproj` - WPF `WinExe`, assembly `fluxo`, version `1.0.4`; composition root, views, view models, migrations, Windows integrations, and update flow.
- `Fluxo.Core/Fluxo.Core.csproj` - entities, DTOs, enums, filters, interfaces, constants, and budgeting rules.
- `Fluxo.Data/Fluxo.Data.csproj` - EF Core context, repositories, unit of work, and scoped data-operation infrastructure.
- `Fluxo.Services/Fluxo.Services.csproj` - persistence services, mapping, validation, backups, logging, notifications, and DPAPI protection.
- `Fluxo.Resources/Fluxo.Resources.csproj` - shared WPF controls, components, converters, themes, fonts, and icons.
- `Fluxo.Installer/Fluxo.Installer.csproj` - WPF managed bootstrapper application, built self-contained for the bundle.
- `Fluxo.Installer.Msi/Fluxo.Installer.Msi.wixproj` - per-machine x64 MSI package.
- `Fluxo.Installer.Bundle/Fluxo.Installer.Bundle.wixproj` - WiX bootstrapper bundle and offline MSI chain.
- `Fluxo.Tests/Fluxo.Tests.csproj` - non-packable xUnit test project covering core logic, data, services, UI structure, installer, and packaging.

## Production Packages

- `AutoMapper` `16.1.1` - entity/DTO/view-model mapping.
- `CommunityToolkit.Mvvm` `8.4.0` - MVVM source generators and messaging in app/resources; installer requests floating `8.*`.
- EF Core data layer: `Microsoft.EntityFrameworkCore`, `Abstractions`, `Analyzers`, `Design`, `Relational`, and `Sqlite` `10.0.9`.
- Main app separately references `Microsoft.EntityFrameworkCore.Design` `10.0.5` for migrations/design tooling.
- `Microsoft.Extensions.Hosting` `10.0.3` - hosting/DI support; runtime composition uses `Microsoft.Extensions.DependencyInjection` APIs.
- `Newtonsoft.Json` `13.0.4` - GitHub release response parsing; backup documents use built-in `System.Text.Json`.
- `FluentValidation` `12.1.1` - validation services.
- `Microsoft.Toolkit.Uwp.Notifications` `7.1.3` - Windows notification support.
- `Serilog` `4.3.1` and `Serilog.Sinks.File` `7.0.0` - categorized local file logging.
- `WixToolset.Sdk` `7.0.0`, `WixToolset.BootstrapperApplicationApi` `7.0.0`, and `WixToolset.BootstrapperApplications.wixext` `7.0.0` - installer toolchain.

## Test Packages

- `Microsoft.NET.Test.Sdk` `18.3.0`.
- `xunit` `2.9.3` and `xunit.runner.visualstudio` `2.8.2`.
- `NSubstitute` `5.3.0`.

## Build, Publish, And Packaging

- Normal restore/build/test use `dotnet` against `Fluxo.slnx` or individual projects.
- `Fluxo/Fluxo.csproj` reorganizes build/publish output: first-party managed DLLs under `libs`, third-party managed DLLs under `vendor`, PDBs and `createdump.exe` removed, and a root hard link retained for the primary assembly.
- MSI build invokes restore/build for the main app as framework-dependent `win-x64`; version defaults to `1.0.4`.
- Bundle build invokes the managed installer as self-contained `win-x64`, generates WiX payload authoring, embeds/caches the MSI, and emits `fluxo-{version}-Installer.exe`.
- `Dockerfile` uses `mcr.microsoft.com/dotnet/sdk:10.0-windowsservercore-ltsc2022`, publishes the app self-contained/single-file for `win-x64`, then runs it on `windows/servercore:ltsc2022`.
- `.github/workflows/build-on-final-commit.yml` restores with .NET `10.0.x`, builds the WiX bundle on `windows-latest`, then tags/releases through GitHub Actions.
- Root `package-lock.json` contains no packages; no JavaScript runtime or frontend build exists.

## Assets And Generated Artifacts

- WPF resources include Urbanist TTF variants, app/installer ICO files, XAML themes/styles, and vector icon resources.
- README screenshots live under `docs/images/`.
- EF migrations and model snapshot live in `Fluxo/Migrations/`; latest migration on this branch is `20260630032006_AddRecurringTransactionEndDate`.
- Build outputs, database files (`*.db`), logs, and binary logs are ignored; `.planning/codebase/*.md` remains tracked content.
