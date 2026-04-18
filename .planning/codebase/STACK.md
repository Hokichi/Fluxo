# Technology Stack

**Analysis Date:** 2026-04-18

## Languages

**Primary:**
- C# (latest, implicit via .NET 10 SDK) - All application source across `Fluxo`, `Fluxo.Core`, `Fluxo.Data`, `Fluxo.Services`, `Fluxo.Tests`
- XAML - WPF UI markup in `Fluxo/Views/`, `Fluxo/Resources/Styles/`, `Fluxo/Resources/Theme.xaml`, `Fluxo/Resources/Icons.xaml`, `Fluxo/App.xaml`

**Secondary:**
- None detected

## Runtime

**Environment:**
- .NET 10 (Windows-specific) - Target framework `net10.0-windows` declared in every `.csproj`:
  - `Fluxo/Fluxo.csproj`
  - `Fluxo.Core/Fluxo.Core.csproj`
  - `Fluxo.Data/Fluxo.Data.csproj`
  - `Fluxo.Services/Fluxo.Services.csproj`
  - `Fluxo.Tests/Fluxo.Tests.csproj`
- WPF (`<UseWPF>true</UseWPF>`) enabled in all projects
- Output type: `WinExe` (`Fluxo/Fluxo.csproj`)

**Package Manager:**
- NuGet (declared via `<PackageReference>` in `.csproj` files)
- Lockfile: not detected (no `packages.lock.json` present)
- Solution descriptor: `Fluxo.slnx` (new XML solution format)

## Frameworks

**Core:**
- WPF (Windows Presentation Foundation) - Desktop UI framework, declared in all `.csproj` files
- Entity Framework Core 10.0.5 - ORM, declared in `Fluxo.Data/Fluxo.Data.csproj`
- Microsoft.Extensions.Hosting 10.0.3 - Generic host / DI bootstrap, declared in `Fluxo/Fluxo.csproj`
- Microsoft.Extensions.DependencyInjection - Used in `Fluxo/App.xaml.cs`, `Fluxo/Extensions/ServiceCollectionExtensions.cs`, `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs`

**Testing:**
- xUnit 2.9.3 - Test framework, declared in `Fluxo.Tests/Fluxo.Tests.csproj`
- xunit.runner.visualstudio 2.8.2 - VS test runner integration
- Microsoft.NET.Test.Sdk 18.3.0 - Test SDK

**Build/Dev:**
- Microsoft.NET.Sdk - Project SDK for all `.csproj`
- Microsoft.EntityFrameworkCore.Design 10.0.5 - EF migrations tooling, present in `Fluxo/Fluxo.csproj` and `Fluxo.Data/Fluxo.Data.csproj`

## Key Dependencies

**Critical:**
- `CommunityToolkit.Mvvm` 8.4.0 - MVVM source generators and `WeakReferenceMessenger`. Registered in `Fluxo/Extensions/ServiceCollectionExtensions.cs`. Underpins all view models in `Fluxo/ViewModels/`.
- `AutoMapper` 16.1.1 - Object-to-object mapping. Configured in `Fluxo/Extensions/ServiceCollectionExtensions.cs` with `EntityDtoProfile` and `DtoViewModelProfile` (`Fluxo.Services/Mappings/`, `Fluxo/Mappings/`).
- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.5 - SQLite provider, configured in `Fluxo.Data/Context/FluxoDbContextFactory.cs`.
- `FluentValidation` 12.1.1 - Validation library, declared in `Fluxo.Services/Fluxo.Services.csproj`.
- `MahApps.Metro.IconPacks` 6.2.1 - Icon library used in WPF views.
- `Serilog` 4.3.1 + `Serilog.Sinks.File` 7.0.0 - Logging dependencies declared in `Fluxo/Fluxo.csproj` (no active configuration code detected; package references only).
- `Newtonsoft.Json` 13.0.4 - JSON serialization, declared in `Fluxo/Fluxo.csproj`.
- `Microsoft.Toolkit.Uwp.Notifications` 7.1.3 - Windows toast notification API, declared in `Fluxo.Services/Fluxo.Services.csproj`.

**Infrastructure:**
- `Microsoft.EntityFrameworkCore` 10.0.5
- `Microsoft.EntityFrameworkCore.Abstractions` 10.0.5
- `Microsoft.EntityFrameworkCore.Analyzers` 10.0.5
- `Microsoft.EntityFrameworkCore.Relational` 10.0.5
- `Microsoft.Extensions.Logging.Abstractions` (transitive, used in `Fluxo/Extensions/ServiceCollectionExtensions.cs` for `NullLoggerFactory.Instance`)

## Configuration

**Environment:**
- No `appsettings.json`, `.env`, or `ConfigurationBuilder` usage detected.
- SQLite database path is computed at runtime in `Fluxo.Data/Context/FluxoDbContextFactory.cs` via `Path.Combine(AppContext.BaseDirectory, "fluxo.db")` -> `Data Source=...\fluxo.db`.
- User-facing settings persist in the database via the `UserSettings` table (`Fluxo.Core/Entities/UserSettings.cs`, `Fluxo.Core/Constants/UserSettingNames.cs`).

**Build:**
- Per-project MSBuild settings (`<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<UseWPF>true</UseWPF>`).
- App icon: `Fluxo/Resources/icon.ico` (referenced via `<ApplicationIcon>` in `Fluxo/Fluxo.csproj`).
- Embedded fonts: ~33 SFT Schrifted Round TTF files declared as `<Resource>` in `Fluxo/Fluxo.csproj` and registered in `Fluxo/App.xaml`.
- Solution file: `Fluxo.slnx`.

## Platform Requirements

**Development:**
- Windows OS (target framework `net10.0-windows` requires Windows for build/test of WPF projects)
- .NET 10 SDK
- Visual Studio or compatible IDE supporting `.slnx` solutions
- EF Core CLI tools for migrations (`dotnet ef`)

**Production:**
- Windows desktop (WPF, `WinExe` output)
- Local SQLite database file (`fluxo.db` placed next to the executable)
- No server, container, or cloud target detected

---

*Stack analysis: 2026-04-18*
