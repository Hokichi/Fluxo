# Analytics Popup — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a full-screen analytics popup reachable from `MainWindow` that shows summary cards, a period bar chart (Expense / Income / All modes), a category donut chart, a top-spending-tags horizontal bar chart, and a savings goals progress panel — all filtered by a user-selected date range.

**Architecture:** A new `IAnalyticsService` queries `ExpenseLogs`, `IncomeLogs`, `ExpenseTags`, `SavingGoals`, and `SpendingSources` in a single pass and returns a flat `AnalyticsDto`. `AnalyticsVM` holds the date range and chart mode; changing the date range triggers a debounced service call that rebuilds all LiveCharts2 series. Changing the chart mode only swaps which series are visible — no new service call. The popup is a transient `Window` opened from a button in the `MainWindow` header.

**Tech Stack:** C# 12, WPF, Community Toolkit MVVM, LiveCharts2 (`LiveChartsCore.SkiaSharpView.WPF`), EF Core via existing `IDataOperationRunner`, NSubstitute + xUnit for tests.

---

## File Map

| Action | Path |
|--------|------|
| Create | `Fluxo.Core/DTO/AnalyticsDto.cs` |
| Create | `Fluxo.Core/Interfaces/Services/IAnalyticsService.cs` |
| Create | `Fluxo.Services/Persistence/AnalyticsService.cs` |
| Create | `Fluxo/ViewModels/Popups/AnalyticsVM.cs` |
| Create | `Fluxo/Views/Popups/AnalyticsPopup.xaml` |
| Create | `Fluxo/Views/Popups/AnalyticsPopup.xaml.cs` |
| Modify | `Fluxo/Views/Shell/Main/MainWindow.xaml` |
| Modify | `Fluxo/Views/Shell/Main/MainWindow.xaml.cs` |
| Modify | `Fluxo/Extensions/ServiceCollectionExtensions.cs` |
| Create | `Fluxo.Tests/Services/AnalyticsServiceTests.cs` |
| Create | `Fluxo.Tests/ViewModels/Popups/AnalyticsVMTests.cs` |

---

## Task 1: Install LiveCharts2

**Files:**
- Modify: `Fluxo/Fluxo.csproj`

- [ ] **Step 1: Add the NuGet package**

```
dotnet add Fluxo/Fluxo.csproj package LiveChartsCore.SkiaSharpView.WPF
```

Expected output: Package `LiveChartsCore.SkiaSharpView.WPF` added successfully.

- [ ] **Step 2: Initialize LiveCharts2 in App.xaml.cs**

In `Fluxo/App.xaml.cs`, add the following call at the top of the `App()` constructor, before the service collection setup:

```csharp
LiveCharts.Configure(config => config
    .AddSkiaSharp()
    .AddDefaultMappers()
    .AddDarkTheme());
```

Add the required usings:

```csharp
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView.WPF;
```

- [ ] **Step 3: Build to verify the package loads correctly**

```
dotnet build Fluxo/Fluxo.csproj -v minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Fluxo/Fluxo.csproj Fluxo/App.xaml.cs
git commit -m "chore: add LiveChartsCore.SkiaSharpView.WPF package"
```

---

## Task 2: AnalyticsDto

**Files:**
- Create: `Fluxo.Core/DTO/AnalyticsDto.cs`

- [ ] **Step 1: Create the DTO file**

```csharp
using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public sealed record AnalyticsDto(
    decimal TotalIncome,
    decimal TotalSpent,
    decimal TotalSpentPriorPeriod,
    IReadOnlyList<CategoryBreakdownItem> CategoryBreakdown,
    IReadOnlyList<TagBreakdownItem> TagBreakdown,
    IReadOnlyList<TimeSeriesItem> TimeSeries,
    IReadOnlyList<SavingGoalProgressItem> SavingGoalsProgress);

public sealed record CategoryBreakdownItem(
    ExpenseCategory Category,
    decimal Total);

public sealed record TagBreakdownItem(
    string TagName,
    string HexCode,
    decimal Total);

public sealed record TimeSeriesItem(
    DateOnly Period,
    decimal Income,
    decimal Expenses);

public sealed record SavingGoalProgressItem(
    string Name,
    decimal CurrentAmount,
    decimal TargetAmount)
{
    public double Percentage =>
        TargetAmount == 0 ? 0 : Math.Min(100d, (double)(CurrentAmount / TargetAmount * 100));
}
```

- [ ] **Step 2: Build to verify**

```
dotnet build Fluxo.Core/Fluxo.Core.csproj -v minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fluxo.Core/DTO/AnalyticsDto.cs
git commit -m "feat: add AnalyticsDto and related record types"
```

---

## Task 3: IAnalyticsService + AnalyticsService

**Files:**
- Create: `Fluxo.Core/Interfaces/Services/IAnalyticsService.cs`
- Create: `Fluxo.Services/Persistence/AnalyticsService.cs`
- Create: `Fluxo.Tests/Services/AnalyticsServiceTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Fluxo.Tests/Services/AnalyticsServiceTests.cs`:

```csharp
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Services.Persistence;
using Fluxo.Tests.TestDoubles;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Services;

public sealed class AnalyticsServiceTests
{
    private static AnalyticsService CreateSut(
        List<ExpenseLog>? expenseLogs = null,
        List<IncomeLog>? incomeLogs = null,
        List<Expense>? expenses = null,
        List<ExpenseTag>? tags = null,
        List<SavingGoal>? goals = null,
        List<SpendingSource>? sources = null)
    {
        var uow = Substitute.For<IUnitOfWork>();

        var expenseLogRepo = Substitute.For<IExpenseLogRepository>();
        expenseLogRepo.GetAllAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<ExpenseLog>>(expenseLogs ?? []));
        uow.ExpenseLogs.Returns(expenseLogRepo);

        var incomeLogRepo = Substitute.For<IIncomeLogRepository>();
        incomeLogRepo.GetAllAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<IncomeLog>>(incomeLogs ?? []));
        uow.IncomeLogs.Returns(incomeLogRepo);

        var expenseRepo = Substitute.For<IExpenseRepository>();
        expenseRepo.GetAllAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<Expense>>(expenses ?? []));
        uow.Expenses.Returns(expenseRepo);

        var tagRepo = Substitute.For<IExpenseTagRepository>();
        tagRepo.GetAllAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<ExpenseTag>>(tags ?? []));
        uow.ExpenseTags.Returns(tagRepo);

        var goalRepo = Substitute.For<ISavingGoalRepository>();
        goalRepo.GetAllAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<SavingGoal>>(goals ?? []));
        uow.SavingGoals.Returns(goalRepo);

        var sourceRepo = Substitute.For<ISpendingSourceRepository>();
        sourceRepo.GetAllAsync(default).ReturnsForAnyArgs(
            Task.FromResult<IReadOnlyList<SpendingSource>>(sources ?? []));
        uow.SpendingSources.Returns(sourceRepo);

        return new AnalyticsService(new InlineDataOperationRunner(uow));
    }

    // ── Summary cards ─────────────────────────────────────────────────────

    [Fact]
    public async Task TotalIncome_SumsIncomeLogsInRange()
    {
        var from = new DateOnly(2026, 4, 1);
        var to = new DateOnly(2026, 4, 30);

        var sut = CreateSut(
            incomeLogs:
            [
                new IncomeLog { Id = 1, Amount = 3000m, AddedOn = new DateTime(2026, 4, 10), SpendingSourceId = 1 },
                new IncomeLog { Id = 2, Amount = 1200m, AddedOn = new DateTime(2026, 4, 25), SpendingSourceId = 1 },
                new IncomeLog { Id = 3, Amount = 500m,  AddedOn = new DateTime(2026, 3, 31), SpendingSourceId = 1 } // out of range
            ]);

        var dto = await sut.GetAnalyticsAsync(from, to);

        Assert.Equal(4200m, dto.TotalIncome);
    }

    [Fact]
    public async Task TotalSpent_SumsExpenseLogsInRangeExcludingSoftDeleted()
    {
        var from = new DateOnly(2026, 4, 1);
        var to = new DateOnly(2026, 4, 30);

        var sut = CreateSut(
            expenseLogs:
            [
                new ExpenseLog { Id = 1, Amount = 100m, DeductedOn = new DateTime(2026, 4, 5),  ExpenseId = 1, SpendingSourceId = 1 },
                new ExpenseLog { Id = 2, Amount = 200m, DeductedOn = new DateTime(2026, 4, 20), ExpenseId = 1, SpendingSourceId = 1 },
                new ExpenseLog { Id = 3, Amount = 999m, DeductedOn = new DateTime(2026, 4, 15), ExpenseId = 1, SpendingSourceId = 1, IsForDeletion = true }, // soft-deleted
                new ExpenseLog { Id = 4, Amount = 50m,  DeductedOn = new DateTime(2026, 3, 31), ExpenseId = 1, SpendingSourceId = 1 }  // out of range
            ]);

        var dto = await sut.GetAnalyticsAsync(from, to);

        Assert.Equal(300m, dto.TotalSpent);
    }

    [Fact]
    public async Task TotalSpentPriorPeriod_CoversSameLengthBeforeStart()
    {
        // Range: Apr 1–30 (30 days). Prior period: Mar 2–31 (30 days).
        var from = new DateOnly(2026, 4, 1);
        var to   = new DateOnly(2026, 4, 30);

        var sut = CreateSut(
            expenseLogs:
            [
                new ExpenseLog { Id = 1, Amount = 400m, DeductedOn = new DateTime(2026, 4, 10), ExpenseId = 1, SpendingSourceId = 1 },
                new ExpenseLog { Id = 2, Amount = 250m, DeductedOn = new DateTime(2026, 3, 15), ExpenseId = 1, SpendingSourceId = 1 } // prior period
            ]);

        var dto = await sut.GetAnalyticsAsync(from, to);

        Assert.Equal(400m, dto.TotalSpent);
        Assert.Equal(250m, dto.TotalSpentPriorPeriod);
    }

    // ── Category breakdown ────────────────────────────────────────────────

    [Fact]
    public async Task CategoryBreakdown_GroupsByExpenseCategory()
    {
        var from = new DateOnly(2026, 4, 1);
        var to   = new DateOnly(2026, 4, 30);

        var sut = CreateSut(
            expenseLogs:
            [
                new ExpenseLog { Id = 1, Amount = 500m, DeductedOn = new DateTime(2026, 4, 5),  ExpenseId = 1, SpendingSourceId = 1 },
                new ExpenseLog { Id = 2, Amount = 100m, DeductedOn = new DateTime(2026, 4, 10), ExpenseId = 2, SpendingSourceId = 1 }
            ],
            expenses:
            [
                new Expense { Id = 1, ExpenseCategory = ExpenseCategory.Needs,   SpendingSourceId = 1, ExpenseTagId = 0, Name = "Rent",    Amount = 500m, ExpenseKind = ExpenseKind.Fixed },
                new Expense { Id = 2, ExpenseCategory = ExpenseCategory.Wants,   SpendingSourceId = 1, ExpenseTagId = 0, Name = "Coffee",  Amount = 100m, ExpenseKind = ExpenseKind.Manual }
            ]);

        var dto = await sut.GetAnalyticsAsync(from, to);

        var needs = dto.CategoryBreakdown.Single(c => c.Category == ExpenseCategory.Needs);
        var wants = dto.CategoryBreakdown.Single(c => c.Category == ExpenseCategory.Wants);
        Assert.Equal(500m, needs.Total);
        Assert.Equal(100m, wants.Total);
    }

    // ── Tag breakdown ─────────────────────────────────────────────────────

    [Fact]
    public async Task TagBreakdown_GroupsByTagSortedDescending()
    {
        var from = new DateOnly(2026, 4, 1);
        var to   = new DateOnly(2026, 4, 30);

        var sut = CreateSut(
            expenseLogs:
            [
                new ExpenseLog { Id = 1, Amount = 1300m, DeductedOn = new DateTime(2026, 4, 5),  ExpenseId = 1, SpendingSourceId = 1 },
                new ExpenseLog { Id = 2, Amount = 117m,  DeductedOn = new DateTime(2026, 4, 10), ExpenseId = 2, SpendingSourceId = 1 }
            ],
            expenses:
            [
                new Expense { Id = 1, ExpenseTagId = 10, ExpenseCategory = ExpenseCategory.Needs, SpendingSourceId = 1, Name = "Electric", Amount = 1300m, ExpenseKind = ExpenseKind.Fixed },
                new Expense { Id = 2, ExpenseTagId = 11, ExpenseCategory = ExpenseCategory.Wants, SpendingSourceId = 1, Name = "Lunch",    Amount = 117m,  ExpenseKind = ExpenseKind.Manual }
            ],
            tags:
            [
                new ExpenseTag { Id = 10, Name = "Utilities", HexCode = "#AAAAAA" },
                new ExpenseTag { Id = 11, Name = "Food",      HexCode = "#FF6600" }
            ]);

        var dto = await sut.GetAnalyticsAsync(from, to);

        Assert.Equal(2, dto.TagBreakdown.Count);
        Assert.Equal("Utilities", dto.TagBreakdown[0].TagName);
        Assert.Equal(1300m, dto.TagBreakdown[0].Total);
        Assert.Equal("Food", dto.TagBreakdown[1].TagName);
        Assert.Equal(117m, dto.TagBreakdown[1].Total);
    }

    // ── Time series ───────────────────────────────────────────────────────

    [Fact]
    public async Task TimeSeries_GroupsByDayWhenRangeUpTo31Days()
    {
        var from = new DateOnly(2026, 4, 1);
        var to   = new DateOnly(2026, 4, 3);

        var sut = CreateSut(
            expenseLogs:
            [
                new ExpenseLog { Id = 1, Amount = 50m, DeductedOn = new DateTime(2026, 4, 1), ExpenseId = 1, SpendingSourceId = 1 },
                new ExpenseLog { Id = 2, Amount = 80m, DeductedOn = new DateTime(2026, 4, 3), ExpenseId = 1, SpendingSourceId = 1 }
            ],
            incomeLogs:
            [
                new IncomeLog { Id = 1, Amount = 1000m, AddedOn = new DateTime(2026, 4, 2), SpendingSourceId = 1 }
            ]);

        var dto = await sut.GetAnalyticsAsync(from, to);

        Assert.Equal(3, dto.TimeSeries.Count);
        Assert.Equal(new DateOnly(2026, 4, 1), dto.TimeSeries[0].Period);
        Assert.Equal(50m,   dto.TimeSeries[0].Expenses);
        Assert.Equal(0m,    dto.TimeSeries[0].Income);
        Assert.Equal(0m,    dto.TimeSeries[1].Expenses);
        Assert.Equal(1000m, dto.TimeSeries[1].Income);
        Assert.Equal(80m,   dto.TimeSeries[2].Expenses);
    }

    [Fact]
    public async Task TimeSeries_GroupsByMonthWhenRangeOver365Days()
    {
        var from = new DateOnly(2025, 1, 1);
        var to   = new DateOnly(2026, 4, 30);

        var sut = CreateSut(
            expenseLogs:
            [
                new ExpenseLog { Id = 1, Amount = 200m, DeductedOn = new DateTime(2025, 1, 15), ExpenseId = 1, SpendingSourceId = 1 },
                new ExpenseLog { Id = 2, Amount = 300m, DeductedOn = new DateTime(2025, 2, 10), ExpenseId = 1, SpendingSourceId = 1 }
            ]);

        var dto = await sut.GetAnalyticsAsync(from, to);

        // Should produce monthly buckets, not daily
        Assert.True(dto.TimeSeries.Count <= 16); // 16 months max
        Assert.All(dto.TimeSeries, item => Assert.Equal(1, item.Period.Day)); // each period starts on day 1
        var jan = dto.TimeSeries.Single(t => t.Period == new DateOnly(2025, 1, 1));
        Assert.Equal(200m, jan.Expenses);
    }

    // ── Saving goals ──────────────────────────────────────────────────────

    [Fact]
    public async Task SavingGoalsProgress_ReturnsAllGoals()
    {
        var from = new DateOnly(2026, 4, 1);
        var to   = new DateOnly(2026, 4, 30);

        var sut = CreateSut(
            goals:
            [
                new SavingGoal { Id = 1, Name = "Emergency Fund", CurrentAmount = 4020m, TargetAmount = 6000m, SavingEndDate = DateTime.Today.AddYears(1) },
                new SavingGoal { Id = 2, Name = "Japan Trip",     CurrentAmount = 1150m, TargetAmount = 5000m, SavingEndDate = DateTime.Today.AddYears(1) }
            ]);

        var dto = await sut.GetAnalyticsAsync(from, to);

        Assert.Equal(2, dto.SavingGoalsProgress.Count);
        var emergency = dto.SavingGoalsProgress.Single(g => g.Name == "Emergency Fund");
        Assert.Equal(4020m, emergency.CurrentAmount);
        Assert.Equal(6000m, emergency.TargetAmount);
        Assert.Equal(67d, Math.Round(emergency.Percentage));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~AnalyticsServiceTests" -v minimal
```

Expected: Build error — `AnalyticsService` does not exist yet.

- [ ] **Step 3: Create the interface**

Create `Fluxo.Core/Interfaces/Services/IAnalyticsService.cs`:

```csharp
using Fluxo.Core.DTO;

namespace Fluxo.Core.Interfaces.Services;

public interface IAnalyticsService
{
    Task<AnalyticsDto> GetAnalyticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Implement the service**

Create `Fluxo.Services/Persistence/AnalyticsService.cs`:

```csharp
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class AnalyticsService(IDataOperationRunner dataOperationRunner) : IAnalyticsService
{
    public async Task<AnalyticsDto> GetAnalyticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var uow = scope.UnitOfWork;

            var allExpenseLogs = await uow.ExpenseLogs.GetAllAsync(ct);
            var allIncomeLogs  = await uow.IncomeLogs.GetAllAsync(ct);
            var allExpenses    = await uow.Expenses.GetAllAsync(ct);
            var allTags        = await uow.ExpenseTags.GetAllAsync(ct);
            var allGoals       = await uow.SavingGoals.GetAllAsync(ct);

            var fromDt = from.ToDateTime(TimeOnly.MinValue);
            var toDt   = to.ToDateTime(TimeOnly.MaxValue);

            var logsInRange = allExpenseLogs
                .Where(l => !l.IsForDeletion && l.DeductedOn >= fromDt && l.DeductedOn <= toDt)
                .ToList();

            var incomeInRange = allIncomeLogs
                .Where(l => l.AddedOn >= fromDt && l.AddedOn <= toDt)
                .ToList();

            // Prior period — same number of days, immediately before the start date.
            var rangeDays  = (to.DayNumber - from.DayNumber) + 1;
            var priorToDt  = fromDt.AddSeconds(-1);
            var priorFromDt = fromDt.AddDays(-rangeDays);
            var priorLogs  = allExpenseLogs
                .Where(l => !l.IsForDeletion && l.DeductedOn >= priorFromDt && l.DeductedOn <= priorToDt)
                .ToList();

            var expenseById = allExpenses.ToDictionary(e => e.Id);
            var tagById     = allTags.ToDictionary(t => t.Id);

            var totalIncome = incomeInRange.Sum(l => l.Amount);
            var totalSpent  = logsInRange.Sum(l => l.Amount);
            var totalSpentPrior = priorLogs.Sum(l => l.Amount);

            var categoryBreakdown = logsInRange
                .GroupBy(l => expenseById.TryGetValue(l.ExpenseId, out var e)
                    ? e.ExpenseCategory
                    : ExpenseCategory.Needs)
                .Select(g => new CategoryBreakdownItem(g.Key, g.Sum(l => l.Amount)))
                .ToList();

            var tagBreakdown = logsInRange
                .GroupBy(l =>
                {
                    var expense = expenseById.GetValueOrDefault(l.ExpenseId);
                    var tag     = expense is not null ? tagById.GetValueOrDefault(expense.ExpenseTagId) : null;
                    return (Name: tag?.Name ?? "Untagged", HexCode: tag?.HexCode ?? "#808080");
                })
                .Select(g => new TagBreakdownItem(g.Key.Name, g.Key.HexCode, g.Sum(l => l.Amount)))
                .OrderByDescending(t => t.Total)
                .ToList();

            var timeSeries = BuildTimeSeries(logsInRange, incomeInRange, from, to, rangeDays);

            var goalProgress = allGoals
                .Select(g => new SavingGoalProgressItem(g.Name, g.CurrentAmount, g.TargetAmount))
                .ToList();

            return new AnalyticsDto(
                TotalIncome: totalIncome,
                TotalSpent: totalSpent,
                TotalSpentPriorPeriod: totalSpentPrior,
                CategoryBreakdown: categoryBreakdown,
                TagBreakdown: tagBreakdown,
                TimeSeries: timeSeries,
                SavingGoalsProgress: goalProgress);
        }, cancellationToken);
    }

    private static IReadOnlyList<TimeSeriesItem> BuildTimeSeries(
        List<ExpenseLog> logs,
        List<IncomeLog> incomeLogs,
        DateOnly from,
        DateOnly to,
        int rangeDays)
    {
        if (rangeDays <= 31)
            return BuildDailySeries(logs, incomeLogs, from, to);

        if (rangeDays <= 365)
            return BuildWeeklySeries(logs, incomeLogs, from, to);

        return BuildMonthlySeries(logs, incomeLogs, from, to);
    }

    private static List<TimeSeriesItem> BuildDailySeries(
        List<ExpenseLog> logs, List<IncomeLog> incomeLogs, DateOnly from, DateOnly to)
    {
        var result = new List<TimeSeriesItem>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            var dayStart = d.ToDateTime(TimeOnly.MinValue);
            var dayEnd   = d.ToDateTime(TimeOnly.MaxValue);
            result.Add(new TimeSeriesItem(
                Period:   d,
                Income:   incomeLogs.Where(l => l.AddedOn >= dayStart && l.AddedOn <= dayEnd).Sum(l => l.Amount),
                Expenses: logs.Where(l => l.DeductedOn >= dayStart && l.DeductedOn <= dayEnd).Sum(l => l.Amount)));
        }
        return result;
    }

    private static List<TimeSeriesItem> BuildWeeklySeries(
        List<ExpenseLog> logs, List<IncomeLog> incomeLogs, DateOnly from, DateOnly to)
    {
        // Align to the Monday on or before 'from'.
        var current = from;
        while (current.DayOfWeek != DayOfWeek.Monday)
            current = current.AddDays(-1);

        var result = new List<TimeSeriesItem>();
        while (current <= to)
        {
            var weekStart = current.ToDateTime(TimeOnly.MinValue);
            var weekEnd   = current.AddDays(6).ToDateTime(TimeOnly.MaxValue);
            result.Add(new TimeSeriesItem(
                Period:   current,
                Income:   incomeLogs.Where(l => l.AddedOn >= weekStart && l.AddedOn <= weekEnd).Sum(l => l.Amount),
                Expenses: logs.Where(l => l.DeductedOn >= weekStart && l.DeductedOn <= weekEnd).Sum(l => l.Amount)));
            current = current.AddDays(7);
        }
        return result;
    }

    private static List<TimeSeriesItem> BuildMonthlySeries(
        List<ExpenseLog> logs, List<IncomeLog> incomeLogs, DateOnly from, DateOnly to)
    {
        var current = new DateOnly(from.Year, from.Month, 1);
        var result  = new List<TimeSeriesItem>();
        while (current <= to)
        {
            var monthStart = current.ToDateTime(TimeOnly.MinValue);
            var nextMonth  = current.AddMonths(1);
            var monthEnd   = nextMonth.ToDateTime(TimeOnly.MinValue).AddSeconds(-1);
            result.Add(new TimeSeriesItem(
                Period:   current,
                Income:   incomeLogs.Where(l => l.AddedOn >= monthStart && l.AddedOn <= monthEnd).Sum(l => l.Amount),
                Expenses: logs.Where(l => l.DeductedOn >= monthStart && l.DeductedOn <= monthEnd).Sum(l => l.Amount)));
            current = nextMonth;
        }
        return result;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~AnalyticsServiceTests" -v minimal
```

Expected: All 8 tests pass.

- [ ] **Step 6: Commit**

```bash
git add Fluxo.Core/Interfaces/Services/IAnalyticsService.cs \
        Fluxo.Services/Persistence/AnalyticsService.cs \
        Fluxo.Tests/Services/AnalyticsServiceTests.cs
git commit -m "feat: add IAnalyticsService and AnalyticsService"
```

---

## Task 4: AnalyticsVM

**Files:**
- Create: `Fluxo/ViewModels/Popups/AnalyticsVM.cs`
- Create: `Fluxo.Tests/ViewModels/Popups/AnalyticsVMTests.cs`

- [ ] **Step 1: Write failing tests**

Create `Fluxo.Tests/ViewModels/Popups/AnalyticsVMTests.cs`:

```csharp
using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Popups;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class AnalyticsVMTests
{
    private static AnalyticsDto MakeDto(
        decimal income = 0m,
        decimal spent = 0m,
        decimal spentPrior = 0m) =>
        new(
            TotalIncome: income,
            TotalSpent: spent,
            TotalSpentPriorPeriod: spentPrior,
            CategoryBreakdown:
            [
                new CategoryBreakdownItem(ExpenseCategory.Needs, spent * 0.8m),
                new CategoryBreakdownItem(ExpenseCategory.Wants, spent * 0.2m)
            ],
            TagBreakdown:
            [
                new TagBreakdownItem("Utilities", "#AAAAAA", spent * 0.5m)
            ],
            TimeSeries:
            [
                new TimeSeriesItem(DateOnly.FromDateTime(DateTime.Today), income, spent)
            ],
            SavingGoalsProgress:
            [
                new SavingGoalProgressItem("Emergency Fund", 4000m, 6000m)
            ]);

    [Fact]
    public async Task LoadAsync_PopulatesSummaryCards()
    {
        var service = Substitute.For<IAnalyticsService>();
        service.GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
               .ReturnsForAnyArgs(MakeDto(income: 4200m, spent: 1711m, spentPrior: 1643m));

        var vm = new AnalyticsVM(service);
        await vm.LoadAsync();

        Assert.Equal("$4,200.00", vm.TotalIncomeText);
        Assert.Equal("$1,711.00", vm.TotalSpentText);
        Assert.Contains("4.1", vm.TotalSpentDeltaText); // (1711 - 1643) / 1643 ≈ +4.1%
        Assert.Equal("$2,489.00", vm.NetSavingsText);
    }

    [Fact]
    public async Task LoadAsync_BuildsExpenseIncomeSeriesFromTimeSeries()
    {
        var service = Substitute.For<IAnalyticsService>();
        service.GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
               .ReturnsForAnyArgs(MakeDto(income: 1000m, spent: 500m));

        var vm = new AnalyticsVM(service);
        await vm.LoadAsync();

        // Default mode is Expense — only one series visible
        Assert.Single(vm.ExpenseIncomeSeries);
    }

    [Fact]
    public async Task ChartMode_All_ShowsTwoSeries()
    {
        var service = Substitute.For<IAnalyticsService>();
        service.GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
               .ReturnsForAnyArgs(MakeDto(income: 1000m, spent: 500m));

        var vm = new AnalyticsVM(service);
        await vm.LoadAsync();

        vm.SelectedChartMode = ChartMode.All;

        Assert.Equal(2, vm.ExpenseIncomeSeries.Length);
    }

    [Fact]
    public async Task ChartMode_Change_DoesNotCallServiceAgain()
    {
        var service = Substitute.For<IAnalyticsService>();
        service.GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
               .ReturnsForAnyArgs(MakeDto());

        var vm = new AnalyticsVM(service);
        await vm.LoadAsync();

        vm.SelectedChartMode = ChartMode.Income;
        vm.SelectedChartMode = ChartMode.All;

        // Service was called exactly once (initial load), never again for mode changes
        await service.Received(1).GetAnalyticsAsync(
            Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TotalSpentDeltaText_ShowsNaWhenNoPriorSpend()
    {
        var service = Substitute.For<IAnalyticsService>();
        service.GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
               .ReturnsForAnyArgs(MakeDto(spent: 500m, spentPrior: 0m));

        var vm = new AnalyticsVM(service);
        await vm.LoadAsync();

        Assert.Equal("n/a", vm.TotalSpentDeltaText);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~AnalyticsVMTests" -v minimal
```

Expected: Build error — `AnalyticsVM` and `ChartMode` do not exist yet.

- [ ] **Step 3: Implement AnalyticsVM**

Create `Fluxo/ViewModels/Popups/AnalyticsVM.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.DTO;
using Fluxo.Core.Interfaces.Services;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Fluxo.ViewModels.Popups;

public enum ChartMode { Expense, Income, All }

public partial class AnalyticsVM : ObservableObject
{
    private readonly IAnalyticsService _analyticsService;
    private CancellationTokenSource _loadCts = new();

    // Cached raw series — set on load, referenced when mode changes.
    private ISeries[] _expenseSeries = [];
    private ISeries[] _incomeSeries  = [];

    [ObservableProperty] private DateTime _startDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _endDate   = DateTime.Today;
    [ObservableProperty] private ChartMode _selectedChartMode = ChartMode.Expense;

    // Summary card texts
    [ObservableProperty] private string _totalIncomeText      = "$0.00";
    [ObservableProperty] private string _totalSpentText       = "$0.00";
    [ObservableProperty] private string _totalSpentDeltaText  = "n/a";
    [ObservableProperty] private string _netSavingsText       = "$0.00";

    // Chart series bound by the view
    [ObservableProperty] private ISeries[] _expenseIncomeSeries = [];
    [ObservableProperty] private ISeries[] _categorySeries      = [];
    [ObservableProperty] private ISeries[] _tagSeries           = [];
    [ObservableProperty] private Axis[]    _timeAxes            = [];
    [ObservableProperty] private Axis[]    _valueAxes           = [new Axis()];
    [ObservableProperty] private Axis[]    _tagValueAxes        = [new Axis()];
    [ObservableProperty] private Axis[]    _tagAxes             = [];

    // Goal progress items bound by ItemsControl
    [ObservableProperty] private IReadOnlyList<SavingGoalProgressItem> _goalProgressItems = [];

    public AnalyticsVM(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    // Called once when the popup opens.
    public Task LoadAsync() => RefreshAsync(debounce: false);

    partial void OnStartDateChanged(DateTime value) => _ = RefreshAsync(debounce: true);
    partial void OnEndDateChanged(DateTime value)   => _ = RefreshAsync(debounce: true);

    partial void OnSelectedChartModeChanged(ChartMode value) => ApplyChartMode();

    private async Task RefreshAsync(bool debounce)
    {
        _loadCts.Cancel();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;

        try
        {
            if (debounce)
                await Task.Delay(300, token);

            var from = DateOnly.FromDateTime(StartDate);
            var to   = DateOnly.FromDateTime(EndDate);

            if (from > to)
                return;

            var dto = await _analyticsService.GetAnalyticsAsync(from, to, token);
            Apply(dto);
        }
        catch (OperationCanceledException) { }
    }

    private void Apply(AnalyticsDto dto)
    {
        // Summary cards
        TotalIncomeText     = $"{dto.TotalIncome:C}";
        TotalSpentText      = $"{dto.TotalSpent:C}";
        NetSavingsText      = $"{dto.TotalIncome - dto.TotalSpent:C}";
        TotalSpentDeltaText = dto.TotalSpentPriorPeriod == 0
            ? "n/a"
            : $"{(dto.TotalSpent - dto.TotalSpentPriorPeriod) / dto.TotalSpentPriorPeriod * 100:+0.#;-0.#}%";

        // Time-series bar chart
        var labels = dto.TimeSeries.Select(t => t.Period.ToString("MMM d")).ToArray();

        _expenseSeries =
        [
            new ColumnSeries<decimal>
            {
                Name   = "Expenses",
                Values = dto.TimeSeries.Select(t => t.Expenses).ToArray(),
                Fill   = new SolidColorPaint(SKColor.Parse("#EF4444"))
            }
        ];

        _incomeSeries =
        [
            new ColumnSeries<decimal>
            {
                Name   = "Income",
                Values = dto.TimeSeries.Select(t => t.Income).ToArray(),
                Fill   = new SolidColorPaint(SKColor.Parse("#22C55E"))
            }
        ];

        TimeAxes = [new Axis { Labels = labels, LabelsRotation = -30 }];

        ApplyChartMode();

        // Donut chart — by category
        CategorySeries = dto.CategoryBreakdown
            .Select(c => (ISeries)new PieSeries<decimal>
            {
                Name       = c.Category.ToString(),
                Values     = [c.Total],
                InnerRadius = 60
            })
            .ToArray();

        // Horizontal bar chart — top spending tags
        var tagLabels  = dto.TagBreakdown.Select(t => t.TagName).ToArray();
        var tagValues  = dto.TagBreakdown.Select(t => t.Total).ToArray();

        TagSeries =
        [
            new RowSeries<decimal>
            {
                Values = tagValues,
                Fill   = new SolidColorPaint(SKColor.Parse("#6366F1"))
            }
        ];

        TagAxes      = [new Axis { Labels = tagLabels }];
        TagValueAxes = [new Axis { MinLimit = 0 }];

        // Goals
        GoalProgressItems = dto.SavingGoalsProgress;
    }

    private void ApplyChartMode()
    {
        ExpenseIncomeSeries = SelectedChartMode switch
        {
            ChartMode.Income  => _incomeSeries,
            ChartMode.All     => [.. _expenseSeries, .. _incomeSeries],
            _                 => _expenseSeries   // Expense (default)
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~AnalyticsVMTests" -v minimal
```

Expected: All 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add Fluxo/ViewModels/Popups/AnalyticsVM.cs \
        Fluxo.Tests/ViewModels/Popups/AnalyticsVMTests.cs
git commit -m "feat: implement AnalyticsVM with chart series and summary cards"
```

---

## Task 5: AnalyticsPopup XAML

**Files:**
- Create: `Fluxo/Views/Popups/AnalyticsPopup.xaml`
- Create: `Fluxo/Views/Popups/AnalyticsPopup.xaml.cs`

- [ ] **Step 1: Create the code-behind**

Create `Fluxo/Views/Popups/AnalyticsPopup.xaml.cs`:

```csharp
using System.Windows;
using Fluxo.ViewModels.Popups;

namespace Fluxo.Views.Popups;

public partial class AnalyticsPopup : Window
{
    private readonly AnalyticsVM _viewModel;

    public AnalyticsPopup(AnalyticsVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        await _viewModel.LoadAsync();
    }
}
```

- [ ] **Step 2: Create the XAML**

Create `Fluxo/Views/Popups/AnalyticsPopup.xaml`:

```xml
<Window
    x:Class="Fluxo.Views.Popups.AnalyticsPopup"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:components="clr-namespace:Fluxo.Views.Components"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:lvc="clr-namespace:LiveChartsCore.SkiaSharpView.WPF;assembly=LiveChartsCore.SkiaSharpView.WPF"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:vm="clr-namespace:Fluxo.ViewModels.Popups"
    d:DataContext="{d:DesignInstance Type=vm:AnalyticsVM}"
    Title="Analytics"
    Width="1200"
    Height="820"
    MinWidth="1000"
    MinHeight="700"
    WindowStartupLocation="CenterOwner"
    WindowStyle="SingleBorderWindow"
    mc:Ignorable="d">

    <Grid Margin="24">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="2*" />
            <RowDefinition Height="*" />
        </Grid.RowDefinitions>

        <!--  Row 0 — Date range pickers  -->
        <StackPanel
            Grid.Row="0"
            Margin="0,0,0,16"
            Orientation="Horizontal"
            VerticalAlignment="Center">
            <TextBlock
                Margin="0,0,8,0"
                VerticalAlignment="Center"
                Foreground="{DynamicResource Brush.Text.Muted}"
                Text="From" />
            <components:DateSelector SelectedDate="{Binding StartDate, Mode=TwoWay}" />
            <TextBlock
                Margin="12,0"
                VerticalAlignment="Center"
                Foreground="{DynamicResource Brush.Text.Muted}"
                Text="To" />
            <components:DateSelector SelectedDate="{Binding EndDate, Mode=TwoWay}" />
        </StackPanel>

        <!--  Row 1 — Summary cards  -->
        <Grid Grid.Row="1" Margin="0,0,0,12">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>

            <Border
                Grid.Column="0"
                Margin="0,0,8,0"
                Padding="16"
                Background="{DynamicResource Brush.Background.Surface}"
                CornerRadius="12">
                <StackPanel>
                    <TextBlock
                        FontSize="11"
                        FontFamily="{DynamicResource Bold}"
                        Foreground="{DynamicResource Brush.Text.Muted}"
                        Text="TOTAL INCOME" />
                    <TextBlock
                        Margin="0,4,0,0"
                        FontSize="26"
                        FontFamily="{DynamicResource Bold}"
                        Foreground="{DynamicResource Brush.Accent.Green}"
                        Text="{Binding TotalIncomeText}" />
                </StackPanel>
            </Border>

            <Border
                Grid.Column="1"
                Margin="0,0,8,0"
                Padding="16"
                Background="{DynamicResource Brush.Background.Surface}"
                CornerRadius="12">
                <StackPanel>
                    <TextBlock
                        FontSize="11"
                        FontFamily="{DynamicResource Bold}"
                        Foreground="{DynamicResource Brush.Text.Muted}"
                        Text="TOTAL SPENT" />
                    <TextBlock
                        Margin="0,4,0,0"
                        FontSize="26"
                        FontFamily="{DynamicResource Bold}"
                        Foreground="{DynamicResource Brush.Accent.Red}"
                        Text="{Binding TotalSpentText}" />
                    <TextBlock
                        FontSize="11"
                        Foreground="{DynamicResource Brush.Text.Muted}"
                        Text="{Binding TotalSpentDeltaText}" />
                </StackPanel>
            </Border>

            <Border
                Grid.Column="2"
                Padding="16"
                Background="{DynamicResource Brush.Background.Surface}"
                CornerRadius="12">
                <StackPanel>
                    <TextBlock
                        FontSize="11"
                        FontFamily="{DynamicResource Bold}"
                        Foreground="{DynamicResource Brush.Text.Muted}"
                        Text="NET SAVINGS" />
                    <TextBlock
                        Margin="0,4,0,0"
                        FontSize="26"
                        FontFamily="{DynamicResource Bold}"
                        Text="{Binding NetSavingsText}" />
                </StackPanel>
            </Border>
        </Grid>

        <!--  Row 2 — Bar chart + Donut  -->
        <Grid Grid.Row="2" Margin="0,0,0,12">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="3*" />
                <ColumnDefinition Width="2*" />
            </Grid.ColumnDefinitions>

            <Border
                Grid.Column="0"
                Margin="0,0,8,0"
                Padding="16"
                Background="{DynamicResource Brush.Background.Surface}"
                CornerRadius="12">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="*" />
                    </Grid.RowDefinitions>

                    <StackPanel Grid.Row="0" Margin="0,0,0,8" Orientation="Horizontal">
                        <RadioButton
                            Content="Expense"
                            IsChecked="{Binding SelectedChartMode,
                                                Converter={StaticResource EnumToBoolConverter},
                                                ConverterParameter=Expense}" />
                        <RadioButton
                            Margin="8,0"
                            Content="Income"
                            IsChecked="{Binding SelectedChartMode,
                                                Converter={StaticResource EnumToBoolConverter},
                                                ConverterParameter=Income}" />
                        <RadioButton
                            Content="All"
                            IsChecked="{Binding SelectedChartMode,
                                                Converter={StaticResource EnumToBoolConverter},
                                                ConverterParameter=All}" />
                    </StackPanel>

                    <lvc:CartesianChart
                        Grid.Row="1"
                        Series="{Binding ExpenseIncomeSeries}"
                        XAxes="{Binding TimeAxes}"
                        YAxes="{Binding ValueAxes}" />
                </Grid>
            </Border>

            <Border
                Grid.Column="1"
                Padding="16"
                Background="{DynamicResource Brush.Background.Surface}"
                CornerRadius="12">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="*" />
                    </Grid.RowDefinitions>
                    <TextBlock
                        Grid.Row="0"
                        Margin="0,0,0,8"
                        FontSize="11"
                        FontFamily="{DynamicResource Bold}"
                        Foreground="{DynamicResource Brush.Text.Muted}"
                        Text="BY CATEGORY" />
                    <lvc:PieChart
                        Grid.Row="1"
                        InitialRotation="-90"
                        Series="{Binding CategorySeries}" />
                </Grid>
            </Border>
        </Grid>

        <!--  Row 3 — Tags + Goals  -->
        <Grid Grid.Row="3">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>

            <Border
                Grid.Column="0"
                Margin="0,0,8,0"
                Padding="16"
                Background="{DynamicResource Brush.Background.Surface}"
                CornerRadius="12">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="*" />
                    </Grid.RowDefinitions>
                    <TextBlock
                        Grid.Row="0"
                        Margin="0,0,0,8"
                        FontSize="11"
                        FontFamily="{DynamicResource Bold}"
                        Foreground="{DynamicResource Brush.Text.Muted}"
                        Text="TOP SPENDING TAGS" />
                    <lvc:CartesianChart
                        Grid.Row="1"
                        Series="{Binding TagSeries}"
                        XAxes="{Binding TagValueAxes}"
                        YAxes="{Binding TagAxes}" />
                </Grid>
            </Border>

            <Border
                Grid.Column="1"
                Padding="16"
                Background="{DynamicResource Brush.Background.Surface}"
                CornerRadius="12">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto" />
                        <RowDefinition Height="*" />
                    </Grid.RowDefinitions>
                    <TextBlock
                        Grid.Row="0"
                        Margin="0,0,0,8"
                        FontSize="11"
                        FontFamily="{DynamicResource Bold}"
                        Foreground="{DynamicResource Brush.Text.Muted}"
                        Text="SAVINGS GOALS PROGRESS" />
                    <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto">
                        <ItemsControl ItemsSource="{Binding GoalProgressItems}">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <StackPanel Margin="0,0,0,14">
                                        <Grid>
                                            <TextBlock
                                                FontFamily="{DynamicResource Bold}"
                                                Text="{Binding Name}" />
                                            <TextBlock
                                                HorizontalAlignment="Right"
                                                FontFamily="{DynamicResource Bold}"
                                                Text="{Binding Percentage, StringFormat={}{0:F0}%}" />
                                        </Grid>
                                        <ProgressBar
                                            Height="8"
                                            Margin="0,4"
                                            Maximum="100"
                                            Value="{Binding Percentage}" />
                                        <Grid>
                                            <TextBlock
                                                FontSize="11"
                                                Foreground="{DynamicResource Brush.Text.Muted}"
                                                Text="{Binding CurrentAmount, StringFormat={}{0:C} saved}" />
                                            <TextBlock
                                                HorizontalAlignment="Right"
                                                FontSize="11"
                                                Foreground="{DynamicResource Brush.Text.Muted}"
                                                Text="{Binding TargetAmount, StringFormat={}{0:C}}" />
                                        </Grid>
                                    </StackPanel>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </ScrollViewer>
                </Grid>
            </Border>
        </Grid>
    </Grid>
</Window>
```

**Note:** The `EnumToBoolConverter` for the RadioButton chart mode binding needs to be registered in App.xaml or a merged resource dictionary. Add a converter class:

Create `Fluxo/Converters/EnumToBoolConverter.cs`:

```csharp
using System.Globalization;
using System.Windows.Data;

namespace Fluxo.Converters;

public sealed class EnumToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string s && Enum.TryParse(targetType, s, out var result))
            return result;
        return Binding.DoNothing;
    }
}
```

Register it in `Fluxo/App.xaml` inside `Application.Resources`:

```xml
<converters:EnumToBoolConverter x:Key="EnumToBoolConverter" />
```

Add the namespace at the top of `App.xaml`:

```xml
xmlns:converters="clr-namespace:Fluxo.Converters"
```

- [ ] **Step 3: Build to verify no errors**

```
dotnet build Fluxo/Fluxo.csproj -v minimal
```

Expected: Build succeeded, 0 errors. If the `EnumToBoolConverter` key can't be found at design time, confirm it is in the correct `ResourceDictionary` in App.xaml.

- [ ] **Step 4: Commit**

```bash
git add Fluxo/Views/Popups/AnalyticsPopup.xaml \
        Fluxo/Views/Popups/AnalyticsPopup.xaml.cs \
        Fluxo/Converters/EnumToBoolConverter.cs \
        Fluxo/App.xaml
git commit -m "feat: add AnalyticsPopup XAML, code-behind, and EnumToBoolConverter"
```

---

## Task 6: MainWindow Button + DI Registration

**Files:**
- Modify: `Fluxo/Views/Shell/Main/MainWindow.xaml`
- Modify: `Fluxo/Views/Shell/Main/MainWindow.xaml.cs`
- Modify: `Fluxo/Extensions/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Add analytics button to MainWindow.xaml**

In `Fluxo/Views/Shell/Main/MainWindow.xaml`, find the header `Grid` (Row 0, the one with `Margin="24,0"`) and add an Analytics button. Place it in the right-side column of the header grid — look for the `ColumnDefinition Width="*"` column that holds the window controls (minimize/close), and add the button before them:

```xml
<Button
    HorizontalAlignment="Right"
    VerticalAlignment="Center"
    Command="{Binding OpenAnalyticsCommand}"
    Content="Analytics"
    ToolTip="Open Analytics" />
```

The exact placement depends on the existing header column layout. Add it to whichever right-side column currently holds action buttons or is empty — align it with `HorizontalAlignment="Right"` so it sits flush right in its column.

- [ ] **Step 2: Add OpenAnalyticsCommand to MainWindow.xaml.cs**

In `Fluxo/Views/Shell/Main/MainWindow.xaml.cs`, add a handler that opens the popup. Add the following (find the existing `OnCloseWindow` / `OnMinimizeWindow` pattern and add alongside them):

```csharp
private void OnOpenAnalytics(object sender, ExecutedRoutedEventArgs e)
{
    using var scope = _serviceProvider.CreateScope();
    var popup = scope.ServiceProvider.GetRequiredService<AnalyticsPopup>();
    popup.Owner = this;
    popup.ShowDialog();
}
```

`MainWindow` already receives `IServiceProvider` via DI (check its constructor — if it doesn't, inject it now):

```csharp
// In MainWindow constructor, add IServiceProvider parameter if not already present:
public MainWindow(MainVM viewModel, IServiceProvider serviceProvider)
{
    InitializeComponent();
    DataContext = viewModel;
    _serviceProvider = serviceProvider;
}

private readonly IServiceProvider _serviceProvider;
```

If `MainWindow` uses a `CommandBinding` pattern (like `OnCloseWindow`), bind `OpenAnalyticsCommand` as a `RoutedCommand` in the same style. If `MainVM` should own the command instead, add `[RelayCommand]` to `MainVM` and inject `IServiceProvider` there — follow whichever pattern the existing window controls use.

Add the required using:

```csharp
using Fluxo.Views.Popups;
```

- [ ] **Step 3: Register IAnalyticsService, AnalyticsVM, and AnalyticsPopup in DI**

In `Fluxo/Extensions/ServiceCollectionExtensions.cs`:

In `AddFluxoPresentation()`, add:

```csharp
services.AddTransient<IAnalyticsService, AnalyticsService>();
```

In `AddUIData()`, add:

```csharp
services.AddTransient<AnalyticsVM>();
services.AddTransient<AnalyticsPopup>();
```

- [ ] **Step 4: Build the full solution**

```
dotnet build Fluxo.slnx -v minimal
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Run all tests**

```
dotnet test Fluxo.Tests/Fluxo.Tests.csproj -v minimal
```

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add Fluxo/Views/Shell/Main/MainWindow.xaml \
        Fluxo/Views/Shell/Main/MainWindow.xaml.cs \
        Fluxo/Extensions/ServiceCollectionExtensions.cs
git commit -m "feat: wire AnalyticsPopup into MainWindow and DI"
```
