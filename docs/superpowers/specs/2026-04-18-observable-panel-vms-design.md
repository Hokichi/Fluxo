# Observable Panel VMs Design

**Date:** 2026-04-18
**Goal:** Remove the snapshot pattern from all three panel ViewModels and make all UI-bound collections observable so WPF reacts to user changes in real time.

---

## Context

After the MainVM refactor, panel VMs own their data and load it via `LoadAsync()`. However, several properties are stored and exposed as `IReadOnlyList<T>`, which does not implement `INotifyCollectionChanged`. WPF cannot react to changes in these collections without a full re-bind.

Additionally, the `BudgetAllocationPanelSnapshot` and `NotificationPanelSnapshot` record types add an unnecessary indirection layer — they exist only to batch-pass data into `LoadSnapshot()`, which was the prior test injection point. With tests rewritten to use mocked services and `LoadAsync()`, both the records and the `LoadSnapshot()` methods are removed.

---

## What Changes

### `BudgetAllocationPanelVM`

- **Remove** `BudgetAllocationPanelSnapshot` record
- **Remove** `LoadSnapshot(BudgetAllocationPanelSnapshot)` method
- **Change** `_spendingSources` field: `IReadOnlyList<SpendingSourceVM>` → `ObservableCollection<SpendingSourceVM>`
- **Change** `SpendingSources` property return type: `IReadOnlyList<SpendingSourceVM>` → `ObservableCollection<SpendingSourceVM>`
- **Change** `MainVM.SpendingSources` return type accordingly
- **Update** `LoadAsync()`: fold in the snapshot logic — fetch from services, map, then update `_spendingSources` in-place (Clear + re-add)
- **Update** `Tags` and `OtherTags`: already `ObservableCollection` via `[ObservableProperty]`, updated in-place via existing `LoadTags()` — no type change needed
- **Remove** nullable service fields and null guards; services become required non-nullable constructor parameters
- **Remove** parameterless constructor

`_allExpenseLogs` stays as `List<ExpenseLogVM>` — it is private and used only to populate the `ICollectionView` wrappers (`Needs`, `Wants`, `Invest`), not bound directly.

### `NotificationPanelVM`

- **Remove** `NotificationPanelSnapshot` record
- **Remove** `LoadSnapshot(NotificationPanelSnapshot)` method
- **Update** `LoadAsync()`: fold in the snapshot logic — fetch from services, update private lists, call `RefreshNotifications()`
- `_expenses`, `_expenseLogs`, `_spendingSources` remain `IReadOnlyList` private fields — they are not bound to the UI; they feed notification evaluation logic
- **Remove** nullable service fields and null guards; services become required non-nullable
- **Remove** parameterless constructor

`Notifications` is already `ObservableCollection<NotificationItemVM>` — no type change needed.

### `SavingGoalsPanelVM`

- **Remove** `LoadSnapshot(IEnumerable<SavingGoalVM>)` method
- **Update** `LoadAsync()`: fold in the snapshot filtering logic directly
- `SavingGoals` and `GoalDots` are already `ObservableCollection` — no type change needed
- **Remove** nullable fields and null guards; repository becomes required non-nullable
- **Remove** parameterless constructor

### `MainVM.SpendingSources`

- Return type changes from `IReadOnlyList<SpendingSourceVM>` to `ObservableCollection<SpendingSourceVM>` (no logic change — still forwards to `BudgetPanel.SpendingSources`)

---

## Test Rewrites

Existing tests in `BudgetAllocationPanelVMTests`, `NotificationPanelVMTests`, and `SavingGoalsPanelVMTests` call `LoadSnapshot()` directly. These are rewritten to:

1. Create mocks for required services using `NSubstitute` (already used in the test project)
2. Configure mock return values with test data
3. Construct the VM via the full constructor
4. Call `await vm.LoadAsync()`
5. Assert on observable properties as before

No new test helpers are introduced — mocks replace the snapshot injection point directly.

---

## Files Touched

| Action | File |
|---|---|
| **Modify** | `Fluxo/ViewModels/Shell/Main/BudgetAllocationPanelVM.cs` |
| **Modify** | `Fluxo/ViewModels/Shell/Main/NotificationPanelVM.cs` |
| **Modify** | `Fluxo/ViewModels/Shell/Main/SavingGoalsPanelVM.cs` |
| **Modify** | `Fluxo/ViewModels/Shell/MainVM.cs` |
| **Rewrite** | `Fluxo.Tests/ViewModels/Shell/Main/BudgetAllocationPanelVMTests.cs` |
| **Rewrite** | `Fluxo.Tests/ViewModels/Shell/Main/NotificationPanelVMTests.cs` |
| **Rewrite** | `Fluxo.Tests/ViewModels/Shell/Main/SavingGoalsPanelVMTests.cs` |

---

## Expected Outcome

- No `*Snapshot` record types remain in the codebase
- No `LoadSnapshot()` methods remain on panel VMs
- `SpendingSources` is `ObservableCollection<SpendingSourceVM>` end-to-end
- All panel VM service fields are non-nullable; null guards removed
- All 41 existing tests pass (rewritten to use mocked services)
- Build is clean
