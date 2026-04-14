# Technology Stack

**Analysis Date:** 2026-04-14

## Languages

**Primary:**
- C# - .NET 10.0, used throughout all projects (Fluxo, Fluxo.Core, Fluxo.Data, Fluxo.Services)

**Secondary:**
- XAML - Windows Presentation Foundation (WPF) UI markup for `Fluxo/` project

## Runtime

**Environment:**
- .NET 10.0 (cross-platform runtime for Windows)
- Windows-specific: `net10.0-windows` target framework

**Platform:**
- Windows Desktop application (WinExe)
- WPF (Windows Presentation Foundation) enabled

## Frameworks

**Core Framework:**
- .NET 10.0 SDK - Runtime and compilation

**UI Framework:**
- Windows Presentation Foundation (WPF) - Desktop UI in `Fluxo/`

**ORM & Data:**
- Entity Framework Core 10.0.5 - Database access in `Fluxo.Data/`
  - `Microsoft.EntityFrameworkCore`
  - `Microsoft.EntityFrameworkCore.Sqlite` - SQLite provider

**Testing:**
- Not detected in project files

**Build/Dev:**
- Microsoft.NET.Sdk - Standard .NET project SDK
- EntityFramework Core Design tools for migrations

## Key Dependencies

**Critical:**
- `Microsoft.EntityFrameworkCore` 10.0.5 - ORM for data access
- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.5 - SQLite database provider
- `AutoMapper` 16.1.1 - Object-to-object mapping (Entity to ViewModel in `Fluxo/` and `Fluxo.Services/`)
- `CommunityToolkit.Mvvm` 8.4.0 - MVVM framework for WPF ViewModels
- `FluentValidation` 12.1.1 - Data validation (`Fluxo.Services/`)

**Infrastructure:**
- `Microsoft.Extensions.Hosting` 10.0.3 - Dependency injection and service hosting (`Fluxo/`)
- `Microsoft.Extensions.DependencyInjection` (implicit via Extensions.Hosting)
- `Microsoft.Extensions.Logging.Abstractions` - Logging abstractions
- `Serilog` 4.3.1 - Structured logging framework (`Fluxo/`)
- `Serilog.Sinks.File` 7.0.0 - File logging sink for Serilog (`Fluxo/`)

**Serialization:**
- `Newtonsoft.Json` 13.0.4 - JSON serialization/deserialization (`Fluxo/`)

**UI/Notifications:**
- `Microsoft.Toolkit.Uwp.Notifications` 7.1.3 - Windows notification support (`Fluxo.Services/`)
- `MahApps.Metro.IconPacks` 6.2.1 - Icon packs for WPF UI (`Fluxo/`)

## Configuration

**Project Structure:**
- Solution file: `Fluxo.slnx` - Contains 4 projects

**Database Configuration:**
- SQLite local database file stored at `AppContext.BaseDirectory/fluxo.db`
- Connection string built in `Fluxo.Data/Context/FluxoDbContextFactory.cs`

**Dependency Injection:**
- Extension methods: `Fluxo/Extensions/ServiceCollectionExtensions.cs`
- Services registered in `Program` (implicit via App.xaml.cs startup)
- Multiple service composition layers for ViewModels and Repositories

**Build Configuration:**
- Nullable reference types enabled across all projects
- Implicit usings enabled for modern C# syntax
- WPF integration enabled in UI project

## Project Organization

**Fluxo** - Main application (WinExe)
- Location: `Fluxo/`
- Target: `net10.0-windows`
- Contains: App shell, XAML UI, ViewModels, Migrations, Converters
- Key dependencies: All other projects, EF Core, AutoMapper, MVVM Toolkit, Serilog, Newtonsoft.Json

**Fluxo.Core** - Core entities and interfaces (Class Library)
- Location: `Fluxo.Core/`
- Target: `net10.0-windows`
- Contains: Domain entities, interfaces, constants

**Fluxo.Data** - Data access layer (Class Library)
- Location: `Fluxo.Data/`
- Target: `net10.0-windows`
- Contains: DbContext, repositories, UnitOfWork pattern, database configuration
- Key dependencies: EF Core, Fluxo.Core

**Fluxo.Services** - Business logic services (Class Library)
- Location: `Fluxo.Services/`
- Target: `net10.0-windows`
- Contains: Service implementations, validation, notification services
- Key dependencies: AutoMapper, FluentValidation, Notifications, Fluxo.Core, Fluxo.Data

## Platform Requirements

**Development:**
- Windows OS
- .NET 10.0 SDK
- Visual Studio 2022 or equivalent IDE
- SQLite support (built into EF Core Sqlite provider)

**Production:**
- Windows OS with .NET 10.0 runtime
- Deployed as standalone WinExe executable
- Local SQLite database file (`fluxo.db`)

---

*Stack analysis: 2026-04-14*
