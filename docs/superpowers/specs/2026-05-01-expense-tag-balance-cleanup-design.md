# Expense Tags, Balance Mutation, and Loader Cleanup Design

## Context
This design addresses four defects in expense transaction behavior and UI:
- Expense Detail shows too many tags in view mode.
- Transaction/fixed-expense tag selectors do not expose a top-level `+ Add Tag` action.
- Balance and spent amounts diverge during delete/edit flows.
- Startup cleanup for soft-deleted expense logs is not executed.

A confirmed product rule applies:
- Deleting an expense transaction must restore balance/spent immediately.
- Every transaction creation/modification must update the affected account balance state.

## Scope
In scope:
- Expense detail tag visibility behavior.
- Add-tag action in transaction and fixed-expense tag lists.
- Expense create/edit/delete balance mutation correctness.
- Startup invocation of post-termination cleanup.

Out of scope:
- Refactoring unrelated settings panels.
- Replacing current repository/UoW architecture.
- Non-expense financial projections.

## Requirements
1. Expense Detail in non-edit mode shows only the selected tag.
2. Expense Detail in edit mode shows all non-system tags.
3. Add New Transaction and Add/Edit Fixed Expense expose a first list item labeled `+ Add Tag`.
4. Selecting `+ Add Tag` opens the existing add-tag flow and refreshes/retains selection predictably.
5. Soft-deleting an expense log restores account values immediately.
6. Expense edits always modify the existing expense/log and apply delta updates (no duplicate expense row).
7. `IsForDeletion` rows are excluded from active expense calculations and list derivations.
8. Startup loader executes post-termination cleanup.
9. Cleanup must not re-adjust balances for already-restored soft-deleted logs.

## Design Decisions

### A. Tag visibility split in Expense Detail
- `ExpenseDetailVM` will maintain mode-aware tag projections:
  - View mode: `VisibleTags` contains only `SelectedTag`; overflow is empty.
  - Edit mode: `VisibleTags`/`OverflowTags` show all non-system tags (current split behavior).
- Mode switch (`IsEditing`) reprojects collections without losing `SelectedTag`.

### B. Add-tag action item in selector lists
- Existing tag-option pattern used by fixed expenses (`TagOption`) is extended to transaction tags and aligned across both flows.
- Both Add New Transaction and Add/Edit Fixed Expense will place an action row at index 0 with label `+ Add Tag`.
- Action row selection behavior:
  - Prevents persisting action as selected domain tag.
  - Opens existing `AddTagPopup` via current dialog service.
  - Reloads non-system tags after popup closes.
  - Auto-selects newly added tag when discoverable.

### C. Balance mutation rules (single source of truth)
Create shared rules for expense impact:
- `ApplyExpense(source, amount)`:
  - Credit/BNPL -> `SpentAmount += amount`
  - Cash/Checking/Saving -> `Balance -= amount`
- `RevertExpense(source, amount)`:
  - Credit/BNPL -> `SpentAmount = max(0, SpentAmount - amount)`
  - Cash/Checking/Saving -> `Balance += amount`
- Edit path:
  - Always revert old source/amount first.
  - Apply new source/amount second.
  - Persist both affected sources when source changes.

Usage points:
- `QuickAddVM` (expense/goal expense-log creation).
- `ExpenseDetailVM.SaveAsync` (expense log edit).
- `ExpenseLogService.DeleteAsync` (soft-delete restore).

### D. Deletion and cleanup contract
- Soft-delete (`ExpenseLogService.DeleteAsync`):
  - Restore source amount immediately.
  - Mark log `IsForDeletion = true`.
- Hard cleanup (`ExpenseLogService.PostTerminationCleanupAsync`):
  - Remove marked logs.
  - Remove orphaned expenses (no remaining logs).
  - Do not restore balances again.

### E. `IsForDeletion` filtering
- Ensure all read paths used for budget/allowance/tag aggregation continue excluding `IsForDeletion` logs.
- Ensure services that return active transactional sets for UI calculations apply this filter where missing.

### F. Loader integration
- In `App.OnStartup`, execute:
  1. DB migration
  2. first-run setting gate
  3. `ExpenseLogService.PostTerminationCleanupAsync`
  4. dashboard initialization
- Cleanup runs under existing startup loader progression and exception boundary.

## Component-Level Changes
- `Fluxo/ViewModels/Popups/ExpenseDetailVM.cs`
  - Mode-aware tag projection logic.
- `Fluxo/Views/Popups/ExpenseDetailPopup.xaml`
  - Bindings remain; behavior driven by VM projection.
- `Fluxo/ViewModels/Popups/QuickAddVM.cs`
  - Add tag-action option support and reload/select flow.
- `Fluxo/Views/Popups/AddNewTransaction.xaml(.cs)`
  - Selector UI updates and add-tag action handling.
- `Fluxo/ViewModels/Popups/AddFixedExpenseVM.cs`
  - Keep/align action option semantics as index-first.
- `Fluxo/Views/Popups/AddFixedExpensePopup.xaml(.cs)`
  - Preserve action-selection handling; ensure placement at top.
- `Fluxo/Services/Persistence/ExpenseLogService.cs`
  - Remove double-restore in cleanup path.
- `Fluxo/Services/Persistence/ExpenseService.cs` and/or shared helper location
  - Consolidate expense balance mutation behavior.
- `Fluxo/App.xaml.cs`
  - Invoke cleanup during loader startup sequence.

## Error Handling
- Add-tag action popup failures surface existing validation/error dialogs; selected domain tag remains unchanged.
- Cleanup failure bubbles to startup error handling (existing app startup exception flow).
- Balance update operations remain transactional through existing unit-of-work boundaries.

## Testing Strategy
Automated tests to add/update:
1. `ExpenseLogService.DeleteAsync` restores immediately for checking and credit/BNPL.
2. `ExpenseLogService.PostTerminationCleanupAsync` removes marked logs and orphan expenses without balance mutation.
3. Expense edit path updates existing records and source balances without creating additional expense entities.
4. Expense Detail view mode only exposes selected tag; edit mode exposes all non-system tags.
5. Add New Transaction and Add/Edit Fixed Expense show `+ Add Tag` as first selectable action and refresh tag list after creation.
6. Startup flow triggers cleanup call once during loader sequence.

## Risks and Mitigations
- Risk: duplicate logic persists across view models.
  - Mitigation: centralize expense impact methods and reuse.
- Risk: tag action row accidentally treated as a real tag.
  - Mitigation: explicit `IsAddTagAction` guard and post-selection rollback.
- Risk: cleanup timing regressions at startup.
  - Mitigation: execute in existing startup stage sequence and cover with tests.

## Rollout
- Implement in a single feature branch commit set.
- Run tests for persistence and popup/view model coverage.
- Validate manual scenarios: create, edit (amount/source change), delete, restart with marked logs.