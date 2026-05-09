# Testing Patterns

**Analysis Date:** 2026-05-09

## Test Stack

- Framework: xUnit `2.9.3`
- Runner: `Microsoft.NET.Test.Sdk` `18.3.0`
- Adapter: `xunit.runner.visualstudio` `2.8.2`
- Mocking: NSubstitute `5.3.0`
- Target framework: `net10.0-windows`
- Test project: `Fluxo.Tests/Fluxo.Tests.csproj`
- Project references: `Fluxo` and `Fluxo.Installer`

## Inventory Snapshot

- Test files: 62 `*Tests.cs`
- Test attributes: 301 `[Fact]`/`[Theory]`
- Tests are centralized in `Fluxo.Tests/` and grouped by product area.

## Test Organization

- `Extensions/`: DI registration checks.
- `Infrastructure/`: data operation runner, service registration, lifetime safety, and single-instance startup policy.
- `Installer/`: installer state machine, operation-mode detection, installed-version lookup, launch command, runtime detection, and WiX authoring checks.
- `Services/`: dialog behavior, notification grouping/actions/summary, UI policy, and selected persistence service behavior.
- `ViewModels/`: dashboard, popup, settings, planning, startup wizard, and custom-control view model logic.
- `Views/`: XAML/layout/style/code-behind guard tests for popups, shell panels, tray popups, custom controls, and helper classes.
- `TestDoubles/`: small in-memory/inline doubles such as `InlineDataOperationRunner`.

## Naming and Test Shape

- Test class names end in `Tests`.
- Test method names usually follow `SubjectOrAction_Condition_ExpectedResult`.
- Async tests return `Task` and await directly.
- Most tests use explicit Arrange/Act/Assert blocks through local setup variables rather than heavy shared fixtures.
- NSubstitute is used for repository, service, dialog, mapper, and unit-of-work boundaries.
- In-memory lists and small fake repositories are common for persistence-oriented tests.
- Some WPF-sensitive tests run code in an STA thread via local `RunInSta` helpers.
- Several UI regression tests assert source or XAML text directly when behavior is otherwise difficult to execute in a headless unit test.

## Commands

```powershell
dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj
dotnet test .\Fluxo.slnx
dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~NotificationPanelVMTests"
dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~Fluxo.Tests.Services.Notifications"
```

Use the test project command for the fastest focused verification. Use the solution command before broad merges when practical.

## CI and Release Workflow

- `.github/workflows/build-on-final-commit.yml` builds and publishes the installer only when a push commit message matches `Final commit for build vX.Y.Z`.
- The observed workflow sets up .NET `10.0.x`, builds `Fluxo.Installer.Bundle`, uploads the installer, tags the commit, and publishes a release.
- No repository workflow was observed that runs `dotnet test`.
- No committed coverage gate, coverlet package, or `.runsettings` coverage profile was observed.

## Coverage Strengths

- Installer flow/state behavior has broad coverage, including install, repair, uninstall, running-app handling, runtime detection, and launch/verification outcomes.
- Notification logic is well covered across grouping, action execution, startup summary, carousel behavior, persistence flags, and dashboard invalidation.
- Dashboard and popup view models have focused coverage for date ranges, budget allocation, saving goals, analytics, settings, planning, quick-add ordering, and command state.
- UI guard tests cover important XAML/style/layout regressions for popups, tray UI, main window helpers, custom controls, progress bindings, and overlay handoff.
- DI/data operation tests verify scoped repository/unit-of-work sharing and expected service lifetimes.

## Coverage Gaps

- Persistence service coverage is uneven; `ExpenseLogService` is covered, but `ExpenseService`, `SpendingSourceService`, `TagService`, `AnalyticsService`, and `AppDataService` have thinner direct coverage.
- EF migrations and startup migration inference logic in `App.xaml.cs` are not directly covered by migration/integration tests.
- End-to-end WPF behavior is mostly represented by view model tests and source/XAML guard tests rather than rendered UI automation.
- No coverage gate exists, so regressions rely on local test discipline.
- Full installer bundle creation is covered by release workflow/build behavior, not regular test execution.

## Guidance for Future Changes

- Add or update tests near the changed feature area before broad refactors.
- For new view model behavior, prefer direct view model tests with substituted services and typed messenger instances.
- For data mutations that span repositories, test through `IDataOperationRunner` or an inline runner/unit-of-work double so transaction boundaries stay visible.
- For persistence services, cover both business effects and soft-delete/deferred-cleanup semantics.
- For XAML-only regressions, keep source/XAML guard tests narrowly targeted to stable resource keys, bindings, and required structural markers.
- For migrations, add tests or scripted verification around upgrade scenarios when changing existing tables, columns, defaults, or migration-history inference.
- Run focused `dotnet test` filters while iterating, then run the full test project before handing off code changes.

---

*Testing analysis refreshed: 2026-05-09*
