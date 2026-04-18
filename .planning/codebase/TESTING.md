# Testing Patterns

**Analysis Date:** 2026-04-18

## Test Framework

**Runner:**
- xUnit 2.9.3
- Test SDK: `Microsoft.NET.Test.Sdk` 18.3.0
- Visual Studio runner: `xunit.runner.visualstudio` 2.8.2
- Config: `Fluxo.Tests/Fluxo.Tests.csproj`
- Target framework: `net10.0-windows` (WPF-aware)

**Assertion Library:**
- xUnit built-in `Assert` (no FluentAssertions / Shouldly)

**Run Commands:**
```bash
dotnet test Fluxo.Tests/Fluxo.Tests.csproj         # Run all tests
dotnet test Fluxo.slnx                              # Run solution-wide
dotnet test --filter "FullyQualifiedName~DateRangeResolver"   # Filter by class
dotnet test --logger "console;verbosity=detailed"   # Verbose output
```

No watch script or coverage tooling is configured — coverlet/ReportGenerator are not referenced.

## Test File Organization

**Location:**
- All tests live in a single sibling project `Fluxo.Tests/`, mirroring the production folder layout under `Fluxo/`.

**Naming:**
- Files: `<TypeUnderTest>Tests.cs`
- Classes: `public [sealed] class <TypeUnderTest>Tests`
- Methods: `<MethodOrScenario>_<Condition>_<ExpectedResult>` (e.g. `Resolve_Weekly_ReturnsMondayToSunday`, `GoNextAsync_OnStep1_DoesNotOverwriteExistingSalarySetting`)

**Structure:**
```
Fluxo.Tests/
├── Fluxo.Tests.csproj
├── Services/
│   └── Dialogs/
│       └── DialogServiceTests.cs
├── ViewModels/
│   ├── Popups/
│   │   ├── GoalUpdateTransactionSupportTests.cs
│   │   └── StartupWizardVMTests.cs
│   └── Shell/Main/
│       ├── BudgetAllocationPanelVMTests.cs
│       ├── DateRangeResolverTests.cs
│       ├── DaySpinnerVMTests.cs
│       ├── MainViewModeToggleVMTests.cs
│       ├── NotificationPanelVMTests.cs
│       └── SavingGoalsPanelVMTests.cs
└── Views/
    └── Shell/Main/
        └── MainWindowShortcutMatcherTests.cs
```

The test namespace mirrors the directory path (e.g. `namespace Fluxo.Tests.ViewModels.Shell.Main;`).

## Test Structure

**Suite Organization:**
Tests use plain xUnit `[Fact]` and `[Theory]` attributes — no shared `IClassFixture` / `ICollectionFixture` are in use. Each test is self-contained and uses Arrange / Act / Assert without explicit comment blocks.

```csharp
// Fluxo.Tests/ViewModels/Shell/Main/DateRangeResolverTests.cs
[Fact]
public void Resolve_Weekly_ReturnsMondayToSunday()
{
    var selected = new DateTime(2026, 4, 19, 14, 30, 0, DateTimeKind.Local);
    var expectedFrom = new DateTime(2026, 4, 13, 0, 0, 0, DateTimeKind.Local);
    var expectedTo = new DateTime(2026, 4, 19, 0, 0, 0, DateTimeKind.Local);

    var result = DateRangeResolver.Resolve(selected, MainContentViewMode.Weekly);

    Assert.Equal(expectedFrom, result.From);
    Assert.Equal(expectedTo, result.To);
}
```

**Theory pattern (parameterized):**
```csharp
// Fluxo.Tests/Views/Shell/Main/MainWindowShortcutMatcherTests.cs
[Theory]
[InlineData(Key.W, ModifierKeys.None)]
[InlineData(Key.W, ModifierKeys.Control | ModifierKeys.Shift)]
[InlineData(Key.S, ModifierKeys.Control)]
public void IsRunSetupWizardShortcut_ReturnsFalse_ForOtherKeysOrModifiers(Key key, ModifierKeys modifiers)
```

**Patterns:**
- Setup: inline within the test, or via private static `CreateSut(...)` / `CreateViewModel(...)` factory methods (see `DialogServiceTests.CreateSut`, `StartupWizardVMTests.CreateViewModel`).
- Teardown: none — no `IDisposable` / `IAsyncLifetime` usage. Tests rely on per-test instances.
- Async tests return `Task` and use `async/await` (e.g. `GoNextAsync_OnStep1_DoesNotOverwriteExistingSalarySetting`).
- Exception assertions use `Assert.Throws<T>` (e.g. `Resolve_AllTime_ThrowsInvalidOperationException` in `DateRangeResolverTests`).

## Mocking

**Framework:** None. No Moq / NSubstitute / FakeItEasy package is referenced in `Fluxo.Tests/Fluxo.Tests.csproj`.

**Approach:** Hand-written test doubles (fakes / stubs) implemented as `private sealed class`es nested inside the test class. Dependencies are abstracted behind interfaces in `Fluxo.Core.Interfaces` (e.g. `IUnitOfWork`, `IUserSettingsRepository`, `IExpenseTagRepository`), which the fakes implement.

**Patterns:**

Hand-rolled fake repository tracking state:
```csharp
// Fluxo.Tests/ViewModels/Popups/StartupWizardVMTests.cs
private sealed class TestUserSettingsRepository(IReadOnlyList<UserSettings> initialSettings) : IUserSettingsRepository
{
    private readonly Dictionary<string, UserSettings> _settings = ...;
    public HashSet<string> AddedNames { get; } = new(StringComparer.Ordinal);
    public HashSet<string> UpdatedNames { get; } = new(StringComparer.Ordinal);
    public HashSet<string> RemovedNames { get; } = new(StringComparer.Ordinal);
    // ... interface members record interactions for assertions
}
```

Unused interface members throw `NotSupportedException` to fail loudly if tests touch them:
```csharp
// Fluxo.Tests/ViewModels/Popups/GoalUpdateTransactionSupportTests.cs
public IExpenseRepository Expenses => throw new NotSupportedException();
public IExpenseLogRepository ExpenseLogs => throw new NotSupportedException();
```

Delegate-based seam for static / framework calls (instead of mocking `MessageBox.Show`):
```csharp
// Fluxo.Tests/Services/Dialogs/DialogServiceTests.cs
var sut = new DialogService(serviceProvider,
    (_, _, _, buttons, icon) =>
    {
        state.LastIcon = icon;
        state.LastButtons = buttons;
        return MessageBoxResult.OK;
    });
```

**What to Mock:**
- Repositories and `IUnitOfWork` — always behind interfaces from `Fluxo.Core.Interfaces.Repositories`.
- WPF/system entry points (e.g. `MessageBox.Show`) via injected delegate parameters on the SUT constructor.

**What NOT to Mock:**
- Pure logic types (e.g. `DateRangeResolver`, `MainWindowShortcutMatcher`, `SavingGoalsPanelVM`) — exercised directly with real inputs.
- DI container itself — `new ServiceCollection().BuildServiceProvider()` is used as a real, empty provider where one is required (`DialogServiceTests`).
- Domain entities (`UserSettings`, `ExpenseTag`, `SavingGoalVM`) — instantiated directly.

## Fixtures and Factories

**Test Data:**
Per-test inline construction is preferred. Where data is repeated, a `private static` helper inside the test class produces it:

```csharp
// Fluxo.Tests/ViewModels/Shell/Main/SavingGoalsPanelVMTests.cs
private static IReadOnlyList<SavingGoalVM> CreateGoals(int count)
{
    return Enumerable.Range(1, count)
        .Select(id => new SavingGoalVM { Id = id, Name = $"Goal {id}", TargetAmount = 1000m, ... })
        .ToList();
}
```

SUT factories:
```csharp
private static (DialogService sut, TestMessageBoxState state) CreateSut() { ... }
private static StartupWizardVM CreateViewModel(TestUnitOfWork? unitOfWork = null) { ... }
```

**Location:**
- No shared `Fixtures/` or `TestData/` directories — fixtures and fakes live as nested `private sealed class`es within the consuming test file.

## Coverage

**Requirements:** None enforced. No `coverlet.collector`, `coverlet.msbuild`, or `.runsettings` file is present.

**View Coverage:**
```bash
# Not configured. To opt in locally:
dotnet test --collect:"XPlat Code Coverage"
```

## Test Types

**Unit Tests:**
- Scope: ViewModels (`Fluxo.Tests/ViewModels/...`), pure helper/service classes (`DateRangeResolver`, `MainWindowShortcutMatcher`, `DialogService`, `GoalUpdateTransactionSupport`).
- Approach: Construct the SUT directly with hand-rolled fakes for dependencies; assert on observable state and recorded interactions.

**Integration Tests:**
- Not present. There are no tests that touch the real EF Core context (`Fluxo.Data`) or the WPF render pipeline.

**E2E / UI Tests:**
- Not used. No FlaUI / WinAppDriver / Appium harness is configured, despite the `net10.0-windows` target.

## Common Patterns

**Async Testing:**
```csharp
// Fluxo.Tests/ViewModels/Popups/StartupWizardVMTests.cs
[Fact]
public async Task GoNextAsync_OnStep1_DoesNotOverwriteExistingSalarySetting()
{
    // ... arrange ...
    var result = await viewModel.GoNextAsync();
    Assert.True(result.IsSuccess);
}
```

**Exception Testing:**
```csharp
// Fluxo.Tests/ViewModels/Shell/Main/DateRangeResolverTests.cs
Assert.Throws<InvalidOperationException>(() =>
    DateRangeResolver.Resolve(selected, MainContentViewMode.AllTime));
```

**Interaction Verification (without mocking framework):**
Fakes expose `HashSet<string>` / counter properties (e.g. `AddedNames`, `UpdatedNames`, `RemovedNames`, `SaveChangesCalls`) so tests can assert on `Assert.DoesNotContain(...)` / `Assert.Equal(1, fake.SaveChangesCalls)`.

**Single-instance assertion:**
```csharp
var remainingGoal = Assert.Single(vm.SavingGoals);
var activeDot = Assert.Single(vm.GoalDots, dot => dot.IsActive);
```

## Notable Gaps

- No tests under `Fluxo.Core.Tests`, `Fluxo.Data.Tests`, or `Fluxo.Services.Tests` — only `Fluxo.Tests` exists, focused on ViewModels + a handful of helpers.
- No code coverage collection configured.
- No CI test runner config detected at the time of analysis.

---

*Testing analysis: 2026-04-18*
