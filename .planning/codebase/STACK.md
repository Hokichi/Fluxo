# Codebase Map: Stack

Generated: 2026-06-15

## Summary

Fluxo is a Windows desktop personal finance application built with .NET 10 and WPF. The repository contains the main app, shared domain and UI resource libraries, persistence and service layers, an installer UI, WiX MSI/bundle packaging, and an xUnit test suite.

## Runtime And Language

- Language: C# with nullable reference types enabled.
- Runtime target: `net10.0-windows`.
- UI framework: WPF via `<UseWPF>true</UseWPF>`.
- Primary platform: Windows x64 via `RuntimeIdentifier` `win-x64`.
- Solution file: `Fluxo.slnx`.

## Projects

- `Fluxo/Fluxo.csproj` - main WPF executable, assembly name `fluxo`, version `1.0.4`.
- `Fluxo.Core/Fluxo.Core.csproj` - entities, DTOs, enums, interfaces, constants, budgeting logic.
- `Fluxo.Data/Fluxo.Data.csproj` - EF Core context, repositories, unit of work, data operation scopes.
- `Fluxo.Services/Fluxo.Services.csproj` - persistence, backup, logging, and mapping services.
- `Fluxo.Resources/Fluxo.Resources.csproj` - shared WPF resources, controls, components, converters, fonts, icons.
- `Fluxo.Installer/Fluxo.Installer.csproj` - managed WiX bootstrapper application.
- `Fluxo.Installer.Msi/Fluxo.Installer.Msi.wixproj` - MSI package authoring.
- `Fluxo.Installer.Bundle/Fluxo.Installer.Bundle.wixproj` - bundle executable authoring.
- `Fluxo.Tests/Fluxo.Tests.csproj` - xUnit tests.

## Main Dependencies

- `AutoMapper` 16.1.1 for entity/DTO/view-model mapping.
- `CommunityToolkit.Mvvm` 8.4.0 in the app and resources projects; installer uses `8.*`.
- `Microsoft.EntityFrameworkCore` 10.0.5 with SQLite provider in `Fluxo.Data/Fluxo.Data.csproj`.
- `Microsoft.Extensions.Hosting` 10.0.3 in `Fluxo/Fluxo.csproj`.
- `Newtonsoft.Json` 13.0.4 in `Fluxo/Fluxo.csproj`.
- `FluentValidation` 12.1.1 in `Fluxo.Services/Fluxo.Services.csproj`.
- `Microsoft.Toolkit.Uwp.Notifications` 7.1.3 in `Fluxo.Services/Fluxo.Services.csproj`.
- `Serilog` 4.3.1 and `Serilog.Sinks.File` 7.0.0 for logging.
- `WixToolset.Sdk/7.0.0` for MSI and bundle projects.
- `WixToolset.BootstrapperApplicationApi` 7.0.0 for the managed installer UI.

## Test Dependencies

- `Microsoft.NET.Test.Sdk` 18.3.0.
- `xunit` 2.9.3.
- `xunit.runner.visualstudio` 2.8.2.
- `NSubstitute` 5.3.0.

## Configuration And Build

- App output is organized by custom MSBuild targets in `Fluxo/Fluxo.csproj`.
- First-party DLLs move to `libs`; vendor DLLs move to `vendor`.
- The main app creates a hard link for `fluxo.exe` after build when needed.
- Publish cleanup removes PDBs and `createdump.exe`, then separates managed libraries.
- Installer MSI builds `Fluxo/Fluxo.csproj` before packaging from `Fluxo.Installer.Msi/Fluxo.Installer.Msi.wixproj`.
- Installer bundle builds the managed bootstrapper self-contained from `Fluxo.Installer.Bundle/Fluxo.Installer.Bundle.wixproj`.

## Assets

- App icon: `Fluxo.Resources/Resources/fluxo.ico`.
- Installer icon: `Fluxo.Installer/fluxo.ico`.
- Font family resources: `Fluxo.Resources/Resources/Fonts/*.ttf`, mostly Urbanist.
- README images live in `docs/images/`.

## Generated Or Local Artifacts

- Build outputs are ignored through `.gitignore`.
- Binary logs such as `msbuild.binlog` and `fluxo-tests.binlog` match ignored patterns.
- SQLite databases are ignored by `*.db`.
- `.planning/codebase/*.md` is not ignored.
