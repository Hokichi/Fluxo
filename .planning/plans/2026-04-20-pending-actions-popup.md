# Pending Actions Popup — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** At startup, detect fixed expenses due this month with no existing log entry, and present a stepped "Pending Actions" popup so the user can confirm or skip logging each one.

**Architecture:** `PendingActionsService` queries all fixed expenses and this month's expense logs in memory, returning only truly pending items. `MainVM.Initialize()` stores the result; `App.OnStartup` opens `PendingActionsPopup` (owned by `MainWindow`) immediately after `mainWindow.Show()` if the list is non-empty. The popup steps through each item one at a time via `PendingActionsVM`, which calls `IExpenseLogService.LogFixedExpenseAsync` on confirm and broadcasts `ExpenseDetailUpdatedMessage` to refresh panels.

**Tech Stack:** C# 12, WPF, Community Toolkit MVVM, EF Core via existing `IDataOperationRunner`, NSubstitute + xUnit for tests.

---

## File Map

| Action | Path |
|--------|------|
| Create | `Fluxo.Core/DTO/PendingFixedExpenseDto.cs` |
| Create | `Fluxo.Core/Interfaces/Services/IPendingActionsService.cs` |
| Create | `Fluxo.Services/Persistence/PendingActionsService.cs` |
| Modify | `Fluxo.Core/Interfaces/Services/IExpenseLogService.cs` |
| Modify | `Fluxo.Services/Persistence/ExpenseLogService.cs` |
| Modify | `Fluxo/ViewModels/Shell/Main/MainVM.cs` |
| Create | `Fluxo/ViewModels/Popups/PendingActionsVM.cs` |
| Create | `Fluxo/Views/Popups/PendingActionsPopup.xaml` |
| Create | `Fluxo/Views/Popups/PendingActionsPopup.xaml.cs` |
| Modify | `Fluxo/Extensions/ServiceCollectionExtensions.cs` |
| Modify | `Fluxo/App.xaml.cs` |
| Create | `Fluxo.Tests/Services/PendingActionsServiceTests.cs` |
| Create | `Fluxo.Tests/Services/ExpenseLogServiceLogFixedTests.cs` |
| Create | `Fluxo.Tests/ViewModels/Popups/PendingActionsVMTests.cs` |

---

## Task 1: PendingFixedExpenseDto

**Files:**
- Create: `Fluxo.Core/DTO/PendingFixedExpenseDto.cs`

- [ ] **Step 1: Create the DTO**

```csharp
using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public sealed record PendingFixedExpenseDto(
    int ExpenseId,
    string Name,
    decimal Amount,
    ExpenseCategory Category,
    string SpendingSourceName,
    int RecurringDate);
```

- [ ] **Step 2: Build to verify no errors**

```
dotnet build Fluxo.Core/Fluxo.Core.csproj -v minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fluxo.Core/DTO/PendingFixedExpenseDto.cs
git commit -m "feat: add PendingFixedExpenseDto"
```

---

## Task 2: IPendingActionsService + PendingActionsService

**Files:**
- Create: `Fluxo.Core/Interfaces/Services/IPendingActionsService.cs`
- Create: `Fluxo.Services/Persistence/PendingActionsService.cs`
- Create: `Fluxo.Tests/Services/PendingActionsServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Fluxo.Tests/Services/PendingActionsServiceTests.cs`:

```csharp
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Services.Persistence;
using Fluxo.Tests.TestDoubles;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Services;

public sealed class PendingActionsServiceTests
{
    private static (PendingActionsService sut, IUnitOfWork uow) CreateSut(
        List<Expense>? expenses = null,
        List<ExpenseLog>? logs = null,
        List<SpendingSource>? sources = null)
    {
        var uow = Substitute.For<IUnitOfWork>();

        var expenseRepo = Substitute.For<IExpenseRepository>();
        expenseRepo.GetAllAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<Expense>>(expenses ?? []));
        uow.Expenses.Returns(expenseRepo);

        var logRepo = Substitute.For<IExpenseLogRepository>();
        logRepo.GetAllAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<ExpenseLog>>(logs ?? []));
        uow.ExpenseLogs.Returns(logRepo);

        var sourceRepo = Substitute.For<ISpendingSourceRepository>();
        sourceRepo.GetAllAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<SpendingSource>>(sources ?? []));
        uow.SpendingSources.Returns(sourceRepo);

        var runner = new InlineDataOperationRunner(uow);
        return (new PendingActionsService(runner), uow);
    }

    [Fact]
    public async Task ReturnsFixedExpenseWithNoLogThisMonth()
    {
        var today = DateTime.Today;
        var expense = new Expense
        {
            Id = 1, Name = "Rent", Amount = 1000m,
            ExpenseKind = ExpenseKind.Fixed, IsActive = true,
            RecurringDate = today.Day, ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = 10
        };
        var source = new SpendingSource { Id = 10, Name = "Checking" };

        var (sut, _) = CreateSut([expense], [], [source]);

        var result = await sut.GetPendingFixedExpensesAsync();

        Assert.Single(result);
        Assert.Equal(1, result[0].ExpenseId);
        Assert.Equal("Rent", result[0].Name);
        Assert.Equal("Checking", result[0].SpendingSourceName);
        Assert.Equal(today.Day, result[0].RecurringDate);
    }

    [Fact]
    public async Task ExcludesExpenseAlreadyLoggedThisMonth()
    {
        var today = DateTime.Today;
        var expense = new Expense
        {
            Id = 1, Name = "Rent", Amount = 1000m,
            ExpenseKind = ExpenseKind.Fixed, IsActive = true,
            RecurringDate = today.Day, ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = 10
        };
        var log = new ExpenseLog
        {
            Id = 1, ExpenseId = 1, Amount = 1000m,
            DeductedOn = new DateTime(today.Year, today.Month, 1),
            SpendingSourceId = 10
        };
        var source = new SpendingSource { Id = 10, Name = "Checking" };

        var (sut, _) = CreateSut([expense], [log], [source]);

        var result = await sut.GetPendingFixedExpensesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExcludesExpenseNotYetDueThisMonth()
    {
        var today = DateTime.Today;
        var expense = new Expense
        {
            Id = 1, Name = "Rent", Amount = 1000m,
            ExpenseKind = ExpenseKind.Fixed, IsActive = true,
            RecurringDate = today.Day + 1, // not yet due
            ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = 10
        };
        var source = new SpendingSource { Id = 10, Name = "Checking" };

        var (sut, _) = CreateSut([expense], [], [source]);

        var result = await sut.GetPendingFixedExpensesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExcludesInactiveExpenses()
    {
        var today = DateTime.Today;
        var expense = new Expense
        {
            Id = 1, Name = "Rent", Amount = 1000m,
            ExpenseKind = ExpenseKind.Fixed, IsActive = false,
            RecurringDate = today.Day, ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = 10
        };
        var source = new SpendingSource { Id = 10, Name = "Checking" };

        var (sut, _) = CreateSut([expense], [], [source]);

        var result = await sut.GetPendingFixedExpensesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExcludesManualExpenses()
    {
        var today = DateTime.Today;
        var expense = new Expense
        {
            Id = 1, Name = "Groceries", Amount = 50m,
            ExpenseKind = ExpenseKind.Manual, IsActive = true,
            RecurringDate = today.Day, ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = 10
        };
        var source = new SpendingSource { Id = 10, Name = "Checking" };

        var (sut, _) = CreateSut([expense], [], [source]);

        var result = await sut.GetPendingFixedExpensesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task ExcludesLogMarkedForDeletion()
    {
        var today = DateTime.Today;
        var expense = new Expense
        {
            Id = 1, Name = "Rent", Amount = 1000m,
            ExpenseKind = ExpenseKind.Fixed, IsActive = true,
            RecurringDate = today.Day, ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = 10
        };
        // Soft-deleted log should NOT count as "already logged"
        var deletedLog = new ExpenseLog
        {
            Id = 1, ExpenseId = 1, Amount = 1000m,
            DeductedOn = new DateTime(today.Year, today.Month, 1),
            SpendingSourceId = 10, IsForDeletion = true
        };
        var source = new SpendingSource { Id = 10, Name = "Checking" };

        var (sut, _) = CreateSut([expense], [deletedLog], [source]);

        var result = await sut.GetPendingFixedExpensesAsync();

        Assert.Single(result);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~PendingActionsServiceTests" -v minimal
```

Expected: Build error — `PendingActionsService` does not exist yet.

- [ ] **Step 3: Create the interface**

Create `Fluxo.Core/Interfaces/Services/IPendingActionsService.cs`:

```csharp
using Fluxo.Core.DTO;

namespace Fluxo.Core.Interfaces.Services;

public interface IPendingActionsService
{
    Task<IReadOnlyList<PendingFixedExpenseDto>> GetPendingFixedExpensesAsync(
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Implement the service**

Create `Fluxo.Services/Persistence/PendingActionsService.cs`:

```csharp
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class PendingActionsService(IDataOperationRunner dataOperationRunner) : IPendingActionsService
{
    public async Task<IReadOnlyList<PendingFixedExpenseDto>> GetPendingFixedExpensesAsync(
        CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var today = DateTime.Today;
            var uow = scope.UnitOfWork;

            var allExpenses = await uow.Expenses.GetAllAsync(ct);
            var allLogs = await uow.ExpenseLogs.GetAllAsync(ct);
            var allSources = await uow.SpendingSources.GetAllAsync(ct);

            var loggedExpenseIdsThisMonth = allLogs
                .Where(l => !l.IsForDeletion
                            && l.DeductedOn.Year == today.Year
                            && l.DeductedOn.Month == today.Month)
                .Select(l => l.ExpenseId)
                .ToHashSet();

            var sourceById = allSources.ToDictionary(s => s.Id);

            return allExpenses
                .Where(e => e.ExpenseKind == ExpenseKind.Fixed
                            && e.IsActive
                            && e.RecurringDate.HasValue
                            && e.RecurringDate.Value <= today.Day
                            && !loggedExpenseIdsThisMonth.Contains(e.Id))
                .Select(e =>
                {
                    var sourceName = sourceById.TryGetValue(e.SpendingSourceId, out var src)
                        ? src.Name
                        : string.Empty;
                    return new PendingFixedExpenseDto(
                        ExpenseId: e.Id,
                        Name: e.Name,
                        Amount: e.Amount,
                        Category: e.ExpenseCategory,
                        SpendingSourceName: sourceName,
                        RecurringDate: e.RecurringDate!.Value);
                })
                .ToList();
        }, cancellationToken);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~PendingActionsServiceTests" -v minimal
```

Expected: All 6 tests pass.

- [ ] **Step 6: Commit**

```bash
git add Fluxo.Core/Interfaces/Services/IPendingActionsService.cs \
        Fluxo.Services/Persistence/PendingActionsService.cs \
        Fluxo.Tests/Services/PendingActionsServiceTests.cs
git commit -m "feat: add IPendingActionsService and PendingActionsService"
```

---

## Task 3: LogFixedExpenseAsync on IExpenseLogService

**Files:**
- Modify: `Fluxo.Core/Interfaces/Services/IExpenseLogService.cs`
- Modify: `Fluxo.Services/Persistence/ExpenseLogService.cs`
- Create: `Fluxo.Tests/Services/ExpenseLogServiceLogFixedTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Fluxo.Tests/Services/ExpenseLogServiceLogFixedTests.cs`:

```csharp
using AutoMapper;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Services.Mappings;
using Fluxo.Services.Persistence;
using Fluxo.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Services;

public sealed class ExpenseLogServiceLogFixedTests
{
    private static (ExpenseLogService sut, IUnitOfWork uow) CreateSut(
        List<Expense>? expenses = null,
        List<SpendingSource>? sources = null)
    {
        var uow = Substitute.For<IUnitOfWork>();

        var expenseRepo = Substitute.For<IExpenseRepository>();
        expenseRepo.GetByExpenseIdAsync(Arg.Any<int>(), default).ReturnsForAnyArgs(ci =>
        {
            var id = ci.Arg<int>();
            return Task.FromResult((expenses ?? []).FirstOrDefault(e => e.Id == id));
        });
        uow.Expenses.Returns(expenseRepo);

        var logRepo = Substitute.For<IExpenseLogRepository>();
        uow.ExpenseLogs.Returns(logRepo);

        var sourceRepo = Substitute.For<ISpendingSourceRepository>();
        sourceRepo.GetByIdAsync(Arg.Any<int>(), default).ReturnsForAnyArgs(ci =>
        {
            var id = ci.Arg<int>();
            return Task.FromResult((sources ?? []).FirstOrDefault(s => s.Id == id));
        });
        uow.SpendingSources.Returns(sourceRepo);

        uow.SaveChangesAsync(default).ReturnsForAnyArgs(Task.FromResult(1));

        var mapperConfig = new MapperConfiguration(cfg =>
            cfg.AddProfile<EntityDtoProfile>(), NullLoggerFactory.Instance);
        var mapper = mapperConfig.CreateMapper();
        var runner = new InlineDataOperationRunner(uow);
        return (new ExpenseLogService(runner, mapper), uow);
    }

    [Fact]
    public async Task LogFixedExpenseAsync_CreatesExpenseLog()
    {
        var expense = new Expense
        {
            Id = 1, Name = "Rent", Amount = 1000m,
            ExpenseKind = ExpenseKind.Fixed,
            ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = 10, ExpenseTagId = 0, IsActive = true
        };
        var source = new SpendingSource
        {
            Id = 10, Name = "Checking", Balance = 5000m, SpentAmount = 0m
        };

        var (sut, uow) = CreateSut([expense], [source]);

        await sut.LogFixedExpenseAsync(1);

        await uow.ExpenseLogs.Received(1).AddAsync(
            Arg.Is<ExpenseLog>(l =>
                l.ExpenseId == 1
                && l.Amount == 1000m
                && l.SpendingSourceId == 10
                && l.DeductedOn.Date == DateTime.Today),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogFixedExpenseAsync_DeductsFromSpendingSourceBalance()
    {
        var expense = new Expense
        {
            Id = 1, Name = "Rent", Amount = 1000m,
            ExpenseKind = ExpenseKind.Fixed,
            ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = 10, ExpenseTagId = 0, IsActive = true
        };
        var source = new SpendingSource
        {
            Id = 10, Name = "Checking", Balance = 5000m, SpentAmount = 200m
        };

        var (sut, uow) = CreateSut([expense], [source]);

        await sut.LogFixedExpenseAsync(1);

        uow.SpendingSources.Received(1).Update(
            Arg.Is<SpendingSource>(s =>
                s.Balance == 4000m
                && s.SpentAmount == 1200m));
    }

    [Fact]
    public async Task LogFixedExpenseAsync_SavesChanges()
    {
        var expense = new Expense
        {
            Id = 1, Name = "Rent", Amount = 1000m,
            ExpenseKind = ExpenseKind.Fixed,
            ExpenseCategory = ExpenseCategory.Needs,
            SpendingSourceId = 10, ExpenseTagId = 0, IsActive = true
        };
        var source = new SpendingSource
        {
            Id = 10, Name = "Checking", Balance = 5000m, SpentAmount = 0m
        };

        var (sut, uow) = CreateSut([expense], [source]);

        await sut.LogFixedExpenseAsync(1);

        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogFixedExpenseAsync_ThrowsWhenExpenseNotFound()
    {
        var (sut, _) = CreateSut([], []);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.LogFixedExpenseAsync(99));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~ExpenseLogServiceLogFixed" -v minimal
```

Expected: Build error — `LogFixedExpenseAsync` does not exist yet.

- [ ] **Step 3: Add method to IExpenseLogService**

In `Fluxo.Core/Interfaces/Services/IExpenseLogService.cs`, add the new method:

```csharp
using Fluxo.Core.DTO;

namespace Fluxo.Core.Interfaces.Services;

public interface IExpenseLogService
{
    Task<IReadOnlyList<ExpenseLogDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ExpenseLogDto?> GetByLogIdAsync(int id, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task PostTerminationCleanupAsync(CancellationToken cancellationToken = default);
    Task LogFixedExpenseAsync(int expenseId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Implement LogFixedExpenseAsync in ExpenseLogService**

Add the following method to `Fluxo.Services/Persistence/ExpenseLogService.cs` (inside the class, after `PostTerminationCleanupAsync`):

```csharp
public async Task LogFixedExpenseAsync(int expenseId, CancellationToken cancellationToken = default)
{
    await dataOperationRunner.RunAsync(async (scope, ct) =>
    {
        var uow = scope.UnitOfWork;

        var expense = await uow.Expenses.GetByExpenseIdAsync(expenseId, ct)
                      ?? throw new InvalidOperationException(
                          $"Fixed expense with id {expenseId} was not found.");

        var log = new ExpenseLog
        {
            ExpenseId = expenseId,
            SpendingSourceId = expense.SpendingSourceId,
            Amount = expense.Amount,
            DeductedOn = DateTime.Today,
            Notes = string.Empty
        };

        await uow.ExpenseLogs.AddAsync(log, ct);

        var source = await uow.SpendingSources.GetByIdAsync(expense.SpendingSourceId, ct);
        if (source is not null)
        {
            source.Balance -= expense.Amount;
            source.SpentAmount += expense.Amount;
            uow.SpendingSources.Update(source);
        }

        await uow.SaveChangesAsync(ct);
    }, cancellationToken);
}
```

- [ ] **Step 5: Run tests to verify they pass**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~ExpenseLogServiceLogFixed" -v minimal
```

Expected: All 4 tests pass.

- [ ] **Step 6: Commit**

```bash
git add Fluxo.Core/Interfaces/Services/IExpenseLogService.cs \
        Fluxo.Services/Persistence/ExpenseLogService.cs \
        Fluxo.Tests/Services/ExpenseLogServiceLogFixedTests.cs
git commit -m "feat: add LogFixedExpenseAsync to IExpenseLogService"
```

---

## Task 4: Update MainVM to Collect Pending Items

**Files:**
- Modify: `Fluxo/ViewModels/Shell/Main/MainVM.cs`

- [ ] **Step 1: Add IPendingActionsService dependency and PendingFixedExpenses property**

In `Fluxo/ViewModels/Shell/Main/MainVM.cs`, update the class as follows. Add the field, update the constructor, add the property, and call the service in `Initialize()`:

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.DTO;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Shell.Main;

public partial class MainVM : ObservableRecipient
{
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly IPendingActionsService _pendingActionsService;
    private bool _isInitialized;

    [ObservableProperty] private string _username = "User";

    public bool IsInitialized => _isInitialized;

    public IReadOnlyList<PendingFixedExpenseDto> PendingFixedExpenses { get; private set; } = [];

    public MainVM(
        IDataOperationRunner dataOperationRunner,
        IPendingActionsService pendingActionsService,
        Main.NotificationPanelVM notificationPanel,
        Main.BudgetAllocationPanelVM budgetPanel,
        Main.SavingGoalsPanelVM savingGoalsPanel,
        Main.DaySpinnerVM daySpinner,
        Main.MainViewModeToggleVM viewModeToggle)
    {
        _dataOperationRunner = dataOperationRunner;
        _pendingActionsService = pendingActionsService;
        NotificationPanel = notificationPanel;
        BudgetPanel = budgetPanel;
        SavingGoalsPanel = savingGoalsPanel;
        DaySpinner = daySpinner;
        ViewModeToggle = viewModeToggle;

        WeakReferenceMessenger.Default.Register<MainVM, UsernameChangedMessage>(this,
            static (recipient, message) => recipient.Username = message.Value);
        WeakReferenceMessenger.Default.Register<MainVM, ExpenseDetailUpdatedMessage>(this,
            static (recipient, message) => recipient.HandleExpenseDetailUpdatedMessage(message));
    }

    public Main.NotificationPanelVM NotificationPanel { get; }
    public Main.BudgetAllocationPanelVM BudgetPanel { get; }
    public Main.SavingGoalsPanelVM SavingGoalsPanel { get; }
    public Main.DaySpinnerVM DaySpinner { get; }
    public Main.MainViewModeToggleVM ViewModeToggle { get; }

    public ObservableCollection<SpendingSourceVM> SpendingSources => BudgetPanel.SpendingSources;

    public void ToggleSpendingSourceFilter(SpendingSourceVM? spendingSource)
    {
        BudgetPanel.ToggleSelectedSpendingSource(spendingSource);
    }

    public async Task Initialize()
    {
        await LoadUserSettingsAsync();
        await Task.WhenAll(
            BudgetPanel.LoadAsync(),
            NotificationPanel.LoadAsync(),
            SavingGoalsPanel.LoadAsync());
        PendingFixedExpenses = await _pendingActionsService.GetPendingFixedExpensesAsync();
        ViewModeToggle.SetSelectedMainContentViewCommand.Execute(
            ViewModeToggle.SelectedMainContentViewMode);
        _isInitialized = true;
    }

    public async Task ReloadCurrentDataAsync()
    {
        await Task.WhenAll(
            BudgetPanel.LoadAsync(),
            NotificationPanel.LoadAsync(),
            SavingGoalsPanel.LoadAsync());
    }

    private async Task LoadUserSettingsAsync()
    {
        var settingsByName = await _dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var settings = await scope.UnitOfWork.UserSettings.GetAllAsync(ct);
            return settings.ToDictionary(s => s.Name, s => s.Value, StringComparer.Ordinal);
        });

        if (settingsByName.TryGetValue(UserSettingNames.PreferredDisplayName, out var name))
        {
            var trimmed = (name ?? string.Empty).Trim();
            Username = trimmed.Length > 0 ? trimmed : "User";
        }
    }

    private void HandleExpenseDetailUpdatedMessage(ExpenseDetailUpdatedMessage message)
    {
        if (!message.Value.HasChanges)
            return;

        _ = ReloadCurrentDataAsync();
    }
}
```

- [ ] **Step 2: Build to verify no errors**

```
dotnet build Fluxo/Fluxo.csproj -v minimal
```

Expected: Build succeeded, 0 errors. (DI registration will be fixed in Task 7.)

- [ ] **Step 3: Commit**

```bash
git add Fluxo/ViewModels/Shell/Main/MainVM.cs
git commit -m "feat: populate PendingFixedExpenses in MainVM.Initialize"
```

---

## Task 5: PendingActionsVM

**Files:**
- Create: `Fluxo/ViewModels/Popups/PendingActionsVM.cs`
- Create: `Fluxo.Tests/ViewModels/Popups/PendingActionsVMTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Fluxo.Tests/ViewModels/Popups/PendingActionsVMTests.cs`:

```csharp
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Popups;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class PendingActionsVMTests
{
    private static PendingActionsVM CreateVm(
        List<PendingFixedExpenseDto> items,
        IExpenseLogService? service = null)
    {
        service ??= Substitute.For<IExpenseLogService>();
        service.LogFixedExpenseAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
               .ReturnsForAnyArgs(Task.CompletedTask);

        var messenger = new WeakReferenceMessenger();
        return new PendingActionsVM(items, service, messenger);
    }

    private static PendingFixedExpenseDto MakeItem(int id, string name = "Rent") =>
        new(id, name, 1000m, ExpenseCategory.Needs, "Checking", 15);

    [Fact]
    public void InitialState_ShowsFirstItem()
    {
        var vm = CreateVm([MakeItem(1), MakeItem(2, "Internet")]);

        Assert.Equal(0, vm.CurrentIndex);
        Assert.Equal(1, vm.CurrentItem.ExpenseId);
        Assert.Equal(2, vm.Total);
        Assert.False(vm.IsLastItem);
    }

    [Fact]
    public void Skip_AdvancesToNextItem()
    {
        var vm = CreateVm([MakeItem(1), MakeItem(2)]);

        vm.SkipCommand.Execute(null);

        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(2, vm.CurrentItem.ExpenseId);
        Assert.True(vm.IsLastItem);
    }

    [Fact]
    public void Skip_OnLastItem_RaisesCloseRequested()
    {
        var vm = CreateVm([MakeItem(1)]);
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        vm.SkipCommand.Execute(null);

        Assert.True(closed);
    }

    [Fact]
    public async Task Confirm_CallsLogFixedExpenseAsync()
    {
        var service = Substitute.For<IExpenseLogService>();
        service.LogFixedExpenseAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
               .ReturnsForAnyArgs(Task.CompletedTask);
        var vm = CreateVm([MakeItem(1)], service);

        await vm.ConfirmCommand.ExecuteAsync(null);

        await service.Received(1).LogFixedExpenseAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_OnLastItem_RaisesCloseRequested()
    {
        var vm = CreateVm([MakeItem(1)]);
        var closed = false;
        vm.CloseRequested += (_, _) => closed = true;

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.True(closed);
    }

    [Fact]
    public async Task Confirm_AdvancesToNextItemWhenNotLast()
    {
        var vm = CreateVm([MakeItem(1), MakeItem(2)]);

        await vm.ConfirmCommand.ExecuteAsync(null);

        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(2, vm.CurrentItem.ExpenseId);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~PendingActionsVMTests" -v minimal
```

Expected: Build error — `PendingActionsVM` does not exist yet.

- [ ] **Step 3: Implement PendingActionsVM**

Create `Fluxo/ViewModels/Popups/PendingActionsVM.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Messages;

namespace Fluxo.ViewModels.Popups;

public partial class PendingActionsVM : ObservableObject
{
    private readonly IExpenseLogService _expenseLogService;
    private readonly IMessenger _messenger;
    private readonly IReadOnlyList<PendingFixedExpenseDto> _items;

    [ObservableProperty] private int _currentIndex;
    [ObservableProperty] private bool _isConfirming;

    public event EventHandler? CloseRequested;

    public PendingActionsVM(
        IReadOnlyList<PendingFixedExpenseDto> items,
        IExpenseLogService expenseLogService,
        IMessenger messenger)
    {
        _items = items;
        _expenseLogService = expenseLogService;
        _messenger = messenger;
    }

    public PendingFixedExpenseDto CurrentItem => _items[_currentIndex];
    public int Total => _items.Count;
    public bool IsLastItem => _currentIndex == _items.Count - 1;

    public string HeaderText => $"Pending Action {_currentIndex + 1} of {_items.Count}";
    public string DueDayText => $"Due on the {CurrentItem.RecurringDate}{GetDaySuffix(CurrentItem.RecurringDate)} of each month";

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private async Task ConfirmAsync(CancellationToken cancellationToken)
    {
        IsConfirming = true;
        try
        {
            var item = _items[_currentIndex];
            await _expenseLogService.LogFixedExpenseAsync(item.ExpenseId, cancellationToken);

            _messenger.Send(new ExpenseDetailUpdatedMessage(
                new ExpenseDetailUpdate(
                    ExpenseLogId: 0,
                    PreviousState: new ExpenseDetailSnapshot(0m, DateTime.Today, item.Category, 0, 0),
                    ChangedFields: ExpenseDetailChangedFields.Amount | ExpenseDetailChangedFields.SpendingSource)));

            Advance();
        }
        finally
        {
            IsConfirming = false;
        }
    }

    private bool CanConfirm() => !IsConfirming;

    [RelayCommand]
    private void Skip() => Advance();

    partial void OnIsConfirmingChanged(bool value) =>
        ConfirmCommand.NotifyCanExecuteChanged();

    private void Advance()
    {
        if (_currentIndex < _items.Count - 1)
        {
            CurrentIndex++;
            OnPropertyChanged(nameof(CurrentItem));
            OnPropertyChanged(nameof(IsLastItem));
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(DueDayText));
        }
        else
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string GetDaySuffix(int day) => day switch
    {
        11 or 12 or 13 => "th",
        _ when day % 10 == 1 => "st",
        _ when day % 10 == 2 => "nd",
        _ when day % 10 == 3 => "rd",
        _ => "th"
    };
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~PendingActionsVMTests" -v minimal
```

Expected: All 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add Fluxo/ViewModels/Popups/PendingActionsVM.cs \
        Fluxo.Tests/ViewModels/Popups/PendingActionsVMTests.cs
git commit -m "feat: implement PendingActionsVM with Confirm/Skip commands"
```

---

## Task 6: PendingActionsPopup XAML

**Files:**
- Create: `Fluxo/Views/Popups/PendingActionsPopup.xaml`
- Create: `Fluxo/Views/Popups/PendingActionsPopup.xaml.cs`

- [ ] **Step 1: Create the popup code-behind**

Create `Fluxo/Views/Popups/PendingActionsPopup.xaml.cs`:

```csharp
using System.Windows;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class PendingActionsPopup : Window
{
    public PendingActionsPopup(PendingActionsVM viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) => Close();
    }
}
```

- [ ] **Step 2: Create the popup XAML**

Create `Fluxo/Views/Popups/PendingActionsPopup.xaml`:

```xml
<Window
    x:Class="Fluxo.Views.Popups.PendingActionsPopup"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:Fluxo.ViewModels.Popups"
    d:DataContext="{d:DesignInstance Type=vm:PendingActionsVM}"
    Title="Pending Actions"
    Width="480"
    Height="380"
    WindowStartupLocation="CenterOwner"
    WindowStyle="SingleBorderWindow"
    ResizeMode="NoResize"
    mc:Ignorable="d">
    <Grid Margin="28">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <!-- Header -->
        <TextBlock
            Grid.Row="0"
            Margin="0,0,0,4"
            FontSize="12"
            Foreground="{DynamicResource Brush.Text.Muted}"
            Text="{Binding HeaderText}" />

        <TextBlock
            Grid.Row="1"
            Margin="0,0,0,20"
            FontSize="20"
            FontFamily="{DynamicResource Bold}"
            Text="{Binding CurrentItem.Name}" />

        <!-- Body -->
        <StackPanel Grid.Row="2" Margin="0,0,0,24">
            <Grid Margin="0,0,0,12">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <TextBlock
                    Grid.Column="0"
                    Width="140"
                    Foreground="{DynamicResource Brush.Text.Muted}"
                    Text="Amount" />
                <TextBlock
                    Grid.Column="1"
                    FontFamily="{DynamicResource Bold}"
                    Text="{Binding CurrentItem.Amount, StringFormat={}{0:C}}" />
            </Grid>

            <Grid Margin="0,0,0,12">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <TextBlock
                    Grid.Column="0"
                    Width="140"
                    Foreground="{DynamicResource Brush.Text.Muted}"
                    Text="Category" />
                <TextBlock
                    Grid.Column="1"
                    Text="{Binding CurrentItem.Category}" />
            </Grid>

            <Grid Margin="0,0,0,12">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <TextBlock
                    Grid.Column="0"
                    Width="140"
                    Foreground="{DynamicResource Brush.Text.Muted}"
                    Text="Account" />
                <TextBlock
                    Grid.Column="1"
                    Text="{Binding CurrentItem.SpendingSourceName}" />
            </Grid>

            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto" />
                    <ColumnDefinition Width="*" />
                </Grid.ColumnDefinitions>
                <TextBlock
                    Grid.Column="0"
                    Width="140"
                    Foreground="{DynamicResource Brush.Text.Muted}"
                    Text="Schedule" />
                <TextBlock
                    Grid.Column="1"
                    Text="{Binding DueDayText}" />
            </Grid>
        </StackPanel>

        <!-- Actions -->
        <StackPanel
            Grid.Row="3"
            HorizontalAlignment="Right"
            Orientation="Horizontal">
            <Button
                Margin="0,0,8,0"
                Command="{Binding SkipCommand}"
                Content="Skip" />
            <Button
                Command="{Binding ConfirmCommand}"
                Content="Confirm" />
        </StackPanel>
    </Grid>
</Window>
```

- [ ] **Step 3: Build to verify no errors**

```
dotnet build Fluxo/Fluxo.csproj -v minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Fluxo/Views/Popups/PendingActionsPopup.xaml \
        Fluxo/Views/Popups/PendingActionsPopup.xaml.cs
git commit -m "feat: add PendingActionsPopup XAML and code-behind"
```

---

## Task 7: DI Registration + App.OnStartup Wiring

**Files:**
- Modify: `Fluxo/Extensions/ServiceCollectionExtensions.cs`
- Modify: `Fluxo/App.xaml.cs`

- [ ] **Step 1: Register IPendingActionsService and PendingActionsPopup in DI**

In `Fluxo/Extensions/ServiceCollectionExtensions.cs`, add the following in `AddFluxoPresentation()` alongside the other service registrations:

```csharp
services.AddTransient<IPendingActionsService, PendingActionsService>();
```

Add the required `using` at the top of the file:

```csharp
using Fluxo.Services.Persistence; // already present — PendingActionsService lives here
```

Also add `PendingActionsVM` and `PendingActionsPopup` registration in `AddUIData()`:

```csharp
services.AddTransient<PendingActionsVM>();
services.AddTransient<PendingActionsPopup>();
```

- [ ] **Step 2: Update App.OnStartup to show PendingActionsPopup**

In `Fluxo/App.xaml.cs`, update `OnStartup` to show the popup after `mainWindow.Show()`. The new `else` block becomes:

```csharp
else
{
    var loaderPopup = new StartupLoaderPopup();
    try
    {
        loaderPopup.Show();
        await _mainVM.Initialize();
    }
    finally
    {
        loaderPopup.CloseLoader();
    }
}

var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
MainWindow = mainWindow;
ShutdownMode = ShutdownMode.OnMainWindowClose;
mainWindow.Show();

if (_mainVM.PendingFixedExpenses.Count > 0)
{
    using var scope = _serviceProvider.CreateScope();
    var vm = scope.ServiceProvider.GetRequiredService<PendingActionsVM>();
    // Inject the already-populated list from MainVM
    var popup = new PendingActionsPopup(
        new PendingActionsVM(
            _mainVM.PendingFixedExpenses,
            scope.ServiceProvider.GetRequiredService<IExpenseLogService>(),
            WeakReferenceMessenger.Default));
    popup.Owner = mainWindow;
    popup.ShowDialog();
}
```

Add the required usings at the top of `App.xaml.cs`:

```csharp
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Views.Popups;
```

- [ ] **Step 3: Build the full solution**

```
dotnet build Fluxo.slnx -v minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run all tests**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj -v minimal
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add Fluxo/Extensions/ServiceCollectionExtensions.cs \
        Fluxo/App.xaml.cs
git commit -m "feat: wire PendingActionsPopup into startup flow"
```
