# Analytics Popup & Pending Actions Popup — Design Spec

**Date:** 2026-04-20  
**Status:** Approved

---

## Overview

Two new features for Fluxo, a local single-user WPF personal finance app:

1. **Analytics Popup** — a full-overlay popup with charts and summary stats for a user-selected date range
2. **Pending Actions Popup** — a stepped confirmation popup shown at startup for fixed expenses that are due but not yet logged this month

---

## Feature A: Analytics Popup

### Entry Point

A new button in the `MainWindow` toolbar/header opens `AnalyticsPopup` as an owned `Window` dialog over `MainWindow`.

### Date Range Controls

At the top of the popup: two `DateSelector` components (reusing the existing control) — **Start Date** and **End Date**. Changing either date immediately triggers a full data refresh. Default range on open: first day of the current month → today.

### Layout — 3 Rows

#### Row 1 — Summary Cards (3 cards)

| Card | Value | Delta |
|------|-------|-------|
| Monthly Income | Sum of `IncomeLog.Amount` in range | — |
| Total Spent | Sum of `ExpenseLog.Amount` in range | % change vs equivalent prior period |
| Net Savings | Income − Total Spent | — |

The "prior period" for the delta is a window of equal length immediately before the selected start date.

#### Row 2 — Period Bar Chart (left, ~60%) + By Category Donut (right, ~40%)

**Bar Chart:**
- Three mode toggle buttons above the chart: **Expense / Income / All**
  - `Expense`: one bar series using `ExpenseLog` totals per period
  - `Income`: one bar series using `IncomeLog` totals per period
  - `All`: grouped bars — expense series + income series side by side
- Auto-granularity based on selected range:
  - ≤ 31 days → group by day
  - ≤ 365 days → group by week
  - \> 365 days → group by month

**Donut Chart:**
- Always shows expense breakdown by `ExpenseCategory` (Needs / Wants / Savings)
- Center label: total spent amount
- Legend lists each category with amount and percentage

#### Row 3 — Top Spending Tags (left) + Savings Goals Progress (right)

**Top Spending Tags:**
- Horizontal bar per tag, sorted descending by total `ExpenseLog` amount in range
- Tag color dot (existing tag color) + tag name + amount label
- Tags with $0 spending in range are shown last, grayed out

**Savings Goals Progress:**
- One row per active `SavingGoal`
- Shows: goal name, amount saved, target amount, progress bar (colored per goal), percentage label
- Pulled from existing `SavingGoal` data (not date-range filtered — reflects current all-time progress)

### Architecture

**New files:**
- `Fluxo.Core/DTO/AnalyticsDto.cs` — contains:
  - `decimal TotalIncome`
  - `decimal TotalSpent`
  - `decimal NetSavings`
  - `decimal TotalSpentPriorPeriod` (for delta calculation)
  - `IReadOnlyList<CategoryBreakdownItem>` — `(ExpenseCategory Category, decimal Total)`
  - `IReadOnlyList<TagBreakdownItem>` — `(string TagName, string HexColor, decimal Total)`
  - `IReadOnlyList<TimeSeriesItem>` — `(DateOnly Period, decimal Income, decimal Expenses)`
  - `IReadOnlyList<SavingGoalProgressItem>` — `(string Name, decimal Saved, decimal Target)`
- `Fluxo.Core/Interfaces/Services/IAnalyticsService.cs`
- `Fluxo.Services/Persistence/AnalyticsService.cs` — implements `GetAnalyticsAsync(DateOnly from, DateOnly to, CancellationToken)`
- `Fluxo/ViewModels/Popups/AnalyticsVM.cs` — `ObservableRecipient`; holds date range, chart mode, all LiveCharts2 series as `ObservableProperty`
- `Fluxo/Views/Popups/AnalyticsPopup.xaml` + `.xaml.cs`

**Charting library:** `LiveChartsCore.SkiaSharpView.WPF` NuGet package.

**Data refresh:** `AnalyticsVM` cancels any in-flight load and calls `GetAnalyticsAsync` whenever Start Date or End Date changes (300ms debounce to prevent hammering on rapid picker input). Chart mode changes (Expense / Income / All) only toggle which series are visible in the VM — no new service call is made, since all three series are already loaded in `AnalyticsDto.TimeSeriesItem`.

**DI:** `IAnalyticsService` registered as transient in `Fluxo/Extensions/ServiceCollectionExtensions.cs`. `AnalyticsVM` registered as transient (popup lifecycle).

---

## Feature B: Pending Actions Popup

### Pending Fixed Expense Definition

A fixed expense is **pending** when all of the following are true:
- `Expense.ExpenseKind == Fixed`
- `Expense.IsActive == true`
- `Expense.RecurringDate <= DateTime.Today.Day`
- No `ExpenseLog` exists for this expense with `DeductedOn` in the current calendar month

### Startup Integration

During `MainVM.Initialize()` (which runs inside the `StartupLoaderPopup` phase), a call to `IPendingActionsService.GetPendingFixedExpensesAsync()` collects all pending fixed expenses. The result is stored on `MainVM` as `IReadOnlyList<PendingFixedExpenseDto>`.

In `App.OnStartup`, immediately after `mainWindow.Show()`, if `MainVM.PendingFixedExpenses` is non-empty, a `PendingActionsPopup` is opened as an owned dialog of `MainWindow`.

### Pending Actions Popup UI

A stepped popup with one fixed expense per step.

**Header:** "Pending Action {current} of {total}"

**Body (per expense):**
- Expense name
- Amount
- Category (Needs / Wants / Savings)
- Spending source name
- Original due day (e.g., "Due on the 15th of each month")

**Actions:**
- **Confirm** — logs the expense and advances to the next step
- **Skip** — advances without logging

**Final step:** "Done" button closes the popup.

If all expenses are skipped, "Done" closes with no changes persisted.

### Confirmation Logic

"Confirm" calls `IExpenseLogService.LogFixedExpenseAsync(expenseId, cancellationToken)` which:
1. Creates an `ExpenseLog` with `Amount = expense.Amount`, `DeductedOn = DateTime.Today`, `SpendingSourceId = expense.SpendingSourceId`
2. Deducts `expense.Amount` from `SpendingSource.Balance`
3. Broadcasts `ExpenseDetailUpdatedMessage` so `BudgetAllocationPanelVM` and other subscribers refresh

### Architecture

**New files:**
- `Fluxo.Core/DTO/PendingFixedExpenseDto.cs` — `(int ExpenseId, string Name, decimal Amount, ExpenseCategory Category, string SpendingSourceName, int RecurringDate)`
- `Fluxo.Core/Interfaces/Services/IPendingActionsService.cs`
- `Fluxo.Services/Persistence/PendingActionsService.cs` — implements `GetPendingFixedExpensesAsync(CancellationToken)`
- `Fluxo/ViewModels/Popups/PendingActionsVM.cs` — holds current step index, list of pending items, Confirm/Skip commands
- `Fluxo/Views/Popups/PendingActionsPopup.xaml` + `.xaml.cs`

**Existing files modified:**
- `Fluxo.Core/Interfaces/Services/IExpenseLogService.cs` — add `LogFixedExpenseAsync(int expenseId, CancellationToken)`
- `Fluxo.Services/Persistence/ExpenseLogService.cs` — implement `LogFixedExpenseAsync`
- `Fluxo/ViewModels/Shell/Main/MainVM.cs` — add `PendingFixedExpenses` property; call `IPendingActionsService` in `Initialize()`
- `Fluxo/App.xaml.cs` — open `PendingActionsPopup` after `mainWindow.Show()` if list is non-empty

**DI:** `IPendingActionsService` registered as transient. `PendingActionsVM` registered as transient.

---

## Out of Scope

- Push/email notifications for pending actions
- Exporting analytics data
- Analytics data caching between sessions
- Recurring expenses that repeat more than once a month
