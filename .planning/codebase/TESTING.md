# Codebase Map: Testing

Generated: 2026-07-01

## Summary

Fluxo has one broad Windows-targeted xUnit project covering domain calculations, EF Core/SQLite behavior, services, view models, WPF controls and XAML contracts, installer logic, packaging, and startup infrastructure. Tests are numerous but run locally only: current GitHub Actions workflow builds releases and does not execute the test project.

## Framework And Project

- Test project: `Fluxo.Tests/Fluxo.Tests.csproj`.
- Target: `net10.0-windows`; tests require a Windows-capable .NET 10 SDK/runtime.
- Framework: xUnit `2.9.3`; runner: `xunit.runner.visualstudio` `2.8.2`; SDK: `Microsoft.NET.Test.Sdk` `18.3.0`.
- Mocking: NSubstitute `5.3.0`.
- Project references: `Fluxo/Fluxo.csproj`, `Fluxo.Services/Fluxo.Services.csproj`, and `Fluxo.Installer/Fluxo.Installer.csproj`; core, data, and resources are reached transitively.
- No `.runsettings`, coverage collector, snapshot framework, UI automation framework, or custom xUnit configuration is present.

## Commands

Run from repository root on Windows:

```powershell
dotnet restore .\Fluxo.slnx
dotnet build .\Fluxo.slnx
dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj
```

Useful focused runs:

```powershell
dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~BudgetAllocationCalculatorTests"
dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~Fluxo.Tests.ViewModels.Popups"
```

Release bundle build, which is build validation rather than a test run:

```powershell
dotnet build .\Fluxo.Installer.Bundle\Fluxo.Installer.Bundle.wixproj --configuration Release -p:FluxoInstallerVersion=X.Y.Z
```

## Layout

- `Fluxo.Tests/Budgeting/`: pure allocation calculator and balancing rules.
- `Fluxo.Tests/Infrastructure/`: migrations, EF model/schema, repositories, DI lifetimes/registrations, data-operation wrapping, database paths, startup logging, and single-instance policy.
- `Fluxo.Tests/Services/`: backups, dialogs, history, logging, notifications, persistence, transactions, UI services, and update flow.
- `Fluxo.Tests/ViewModels/`: popup, settings, shell, dashboard, wizard, and custom-control view-model behavior.
- `Fluxo.Tests/Views/`: WPF dependency properties/control behavior plus source-level XAML, style, layout, popup, shell, tray, and wizard contracts.
- `Fluxo.Tests/Installer/`: runtime detection/installation/ownership, elevation, operation modes, flow state, cleanup, launch commands, and WiX authoring.
- `Fluxo.Tests/Packaging/`: executable and installer naming contracts.
- `Fluxo.Tests/TestDoubles/InlineDataOperationRunner.cs`: executes service callbacks against a supplied substitute unit of work without a real DI scope.
- `Fluxo.Tests/TestSupport/RepositoryPaths.cs`: finds `Fluxo.slnx` and resolves source files; `WindowsPathFixtures.cs` centralizes synthetic Windows paths.

## Test Style

- Test classes normally end in `Tests`; methods usually follow `Member_Scenario_ExpectedResult`.
- `[Fact]` covers single cases; `[Theory]` plus `[InlineData]` covers value matrices and boundary cases.
- Tests use arrange/act/assert blocks without comments or a separate fixture layer unless setup is reused.
- NSubstitute creates service/repository seams with `Substitute.For<T>()`, `Returns(...)`, `Arg.Any<T>()`, and received-call assertions.
- Small handwritten fakes are preferred when behavior is clearer than a mock, especially installer delegates and `InlineDataOperationRunner`.
- Async tests return `Task`; cancellation tokens are matched or passed explicitly when verifying repository/service calls.
- Failure paths use `Assert.Throws`, `Assert.ThrowsAsync`, result-object assertions, and post-condition checks for data safety.

## Domain, Service, And View-Model Tests

- Pure business rules are tested directly with concrete input/output examples (`Fluxo.Tests/Budgeting/BudgetAllocationCalculatorTests.cs`).
- Services are usually given substituted interfaces; data callbacks run inline through `InlineDataOperationRunner` (`Fluxo.Tests/Services/Persistence/CalendarServiceTests.cs`).
- View-model tests assert observable state and command outcomes without opening a window (`Fluxo.Tests/ViewModels/Popups/AccountDetailVMTests.cs`).
- Tests commonly construct current domain entities directly with only scenario-relevant properties.

## Database And Filesystem Tests

- Repository/model tests use SQLite in-memory databases with `UseSqlite("Data Source=:memory:")`; keep the connection open while the test runs (`Fluxo.Tests/Infrastructure/ModelSchemaTests.cs`).
- Migration tests create a uniquely named temporary directory and physical SQLite database, run app migration code, inspect schema, clear pools, and delete the directory in `finally` (`Fluxo.Tests/Infrastructure/AppDatabaseMigrationTests.cs`).
- Backup/export tests use `%TEMP%/fluxo-tests/<guid>` paths. Safety tests verify both expected output and preservation of the original on failure.
- Migration and schema assertions cover the current unified `Transactions` model; old `Expenses`, `ExpenseLogs`, and `IncomeLogs` tables are expected to be absent.

## WPF And XAML Tests

- A small set of tests instantiate controls or inspect dependency-property metadata directly (`Fluxo.Tests/Views/CustomControls/`).
- Most visual tests read `.xaml` or `.xaml.cs` source through `RepositoryPaths`, then assert XML elements, attributes, styles, commands, template parts, or exact source markers.
- XDocument is used where structural checks matter; string and regular-expression checks are used for narrow resource/style contracts (`Fluxo.Tests/Views/Styles/BalloonButtonStyleTests.cs`).
- Read-only WPF bindings require explicit `Mode=OneWay`; focused regression checks exist in `Fluxo.Tests/Views/Popups/ProgressBarBindingModeTests.cs`.
- These tests validate declarative contracts and wiring, not rendered pixels, accessibility behavior, focus traversal, DPI scaling, or full user interaction.

## Installer And Packaging Tests

- Installer tests inject delegates/fakes for process, filesystem, registry, runtime-list, and relaunch behavior instead of mutating the real machine.
- `WindowsPathFixtures` supplies deterministic Windows path strings.
- WiX tests inspect `Fluxo.Installer.Msi/*.wxs`, bundle authoring, and expected output naming rather than installing an MSI in test.
- `.github/workflows/build-on-final-commit.yml` restores and builds the release bundle only when the head commit matches `Final commit for build vX.Y.Z`; it does not run `dotnet test`.

## Coverage And Gaps

- No automated coverage collection or threshold exists.
- No CI job currently runs `Fluxo.Tests`; branch/push regressions can pass CI unless the release build itself fails.
- No end-to-end UI automation, screenshot/golden-image comparison, or installer execution test is present.
- Many WPF regression tests are source-text contracts. They are fast and platform-light but can pass without runtime behavior and can fail after harmless markup/code refactoring.
- Test project is monolithic; all app, WPF, installer, SQLite, and source-inspection tests share one Windows-targeted project.
- Temporary-file tests generally isolate paths, but cleanup patterns vary; new tests should use `try/finally` when files, SQLite pools, or directories could survive a failed assertion.

## Adding Tests

- Place tests in the directory and namespace mirroring production ownership.
- Reuse `RepositoryPaths`, `WindowsPathFixtures`, and `InlineDataOperationRunner` before creating another helper.
- Prefer direct behavior tests. Use XAML/source inspection only for declarative contracts that are otherwise expensive to exercise.
- For EF changes, add or update both model/schema assertions and a migration-to-current-schema test where relevant.
- For read-only view-model properties bound in XAML, assert `Mode=OneWay`.
- Keep each regression focused on one behavior and runnable with `dotnet test` plus a fully qualified-name filter.
