# Codebase Map: Testing

Generated: 2026-06-15

## Summary

Fluxo has a broad xUnit test suite focused on domain calculations, persistence behavior, view-model logic, UI layout helpers, installer behavior, update flow, and infrastructure safety.

## Test Framework

- Test project: `Fluxo.Tests/Fluxo.Tests.csproj`.
- Framework: xUnit.
- Runner: `xunit.runner.visualstudio`.
- Mocking: NSubstitute.
- Test SDK: `Microsoft.NET.Test.Sdk`.
- Test target: `net10.0-windows`.

## Project References

- `Fluxo.Tests/Fluxo.Tests.csproj` references `Fluxo.Installer/Fluxo.Installer.csproj`.
- `Fluxo.Tests/Fluxo.Tests.csproj` references `Fluxo/Fluxo.csproj`.
- `Fluxo.Tests/Fluxo.Tests.csproj` references `Fluxo.Services/Fluxo.Services.csproj`.
- Test access reaches app, installer, services, and transitive core/data/resource projects.

## Test Areas

- Budgeting: `Fluxo.Tests/Budgeting/BudgetAllocationCalculatorTests.cs`.
- Infrastructure: `Fluxo.Tests/Infrastructure/*`.
- Installer: `Fluxo.Tests/Installer/*`.
- Packaging: `Fluxo.Tests/Packaging/ExecutableNamingTests.cs`.
- Services: `Fluxo.Tests/Services/*`.
- ViewModels: `Fluxo.Tests/ViewModels/*`.
- Views: `Fluxo.Tests/Views/*`.
- Test doubles: `Fluxo.Tests/TestDoubles/`.
- Test support: `Fluxo.Tests/TestSupport/`.

## Infrastructure Tests

- Migration and schema coverage exists in `Fluxo.Tests/Infrastructure/AppDatabaseMigrationTests.cs` and `Fluxo.Tests/Infrastructure/ModelSchemaTests.cs`.
- Data operation wrapper coverage exists in `Fluxo.Tests/Infrastructure/DataOperationRunnerTests.cs`.
- DI registration coverage exists in `Fluxo.Tests/Infrastructure/DataServiceRegistrationTests.cs`.
- Lifetime safety coverage exists in `Fluxo.Tests/Infrastructure/LifetimeSafetyTests.cs`.
- Database path coverage exists in `Fluxo.Tests/Infrastructure/DatabaseDirectoryPathTests.cs`.

## UI And View Model Tests

- Main shell tests live under `Fluxo.Tests/Views/Shell/Main/` and `Fluxo.Tests/ViewModels/Shell/Main/`.
- Popup tests live under `Fluxo.Tests/Views/Popups/` and `Fluxo.Tests/ViewModels/Popups/`.
- Custom control tests live under `Fluxo.Tests/Views/CustomControls/`.
- Style coverage tests live under `Fluxo.Tests/Views/Styles/`.
- Tray behavior tests live under `Fluxo.Tests/Views/Shell/Tray/`.

## Installer Tests

- Runtime detector/resolver/installer tests live under `Fluxo.Tests/Installer/`.
- MSI authoring and launch command tests are present.
- Installer flow, elevation relaunch, up-to-date decision, and legacy cleanup services have dedicated tests.

## Service Tests

- Backup/export/restore safety tests live under `Fluxo.Tests/Services/Backups/`.
- Persistence service tests include `Fluxo.Tests/Services/Persistence/ExpenseLogServiceTests.cs` and `CalendarServiceTests.cs`.
- Update service tests live under `Fluxo.Tests/Services/Updates/`.
- Notification tests live under `Fluxo.Tests/Services/Notifications/`.
- UI service tests include startup tray popup display policy.

## Typical Commands

- Restore: `dotnet restore .\Fluxo.slnx`.
- Build: `dotnet build .\Fluxo.slnx`.
- Test: `dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj`.
- Release bundle build: `dotnet build .\Fluxo.Installer.Bundle\Fluxo.Installer.Bundle.wixproj --configuration Release -p:FluxoInstallerVersion=X.Y.Z`.

## Known Testing Risks

- WPF and Windows-specific targets mean tests need a Windows-capable .NET SDK/runtime.
- Installer tests may depend on WiX target behavior.
- UI layout tests cover many helpers, but full interactive UI smoke testing is not evident.
- Tests under `Fluxo.Tests/obj` appeared in file scans; generated build output should not be treated as source.
