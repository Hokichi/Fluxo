# Review Notifications Action Matrix Design

Date: 2026-04-29
Project: Fluxo
Status: Approved for planning

## Goal
Replace checkbox-based `Review Notifications` behavior with per-row action selection and support real processing flows:
- `Ignore` (default): no mutation; item can reappear in a later session if still valid.
- `Paid`: clear notification records only.
- `Process`: execute domain mutation (real payment/expense handling), update balances/spent amounts, create logs/history actions, and clear notifications.

For fixed expenses only, when action is `Process`, show a source picker next to the action selector.

## Scope
In scope:
- `NotificationChecklistActionPopup` UI conversion from checkbox rows to text + segmented 3-choice selector.
- Fixed-expense-only source `ComboBox` visibility and binding when `Process` is selected.
- Notification checklist item/viewmodel contract changes to carry action + optional source selection.
- Notification action service changes to support clear-only and process flows.
- Payment and fixed-expense processing integration with existing account mutation and logging patterns.

Out of scope:
- Refactoring unrelated notification groups.
- Reworking transfer popup UX.
- New persistence tables/columns.

## Recommended Approach
Use a minimal patch in the existing notification flow:
1. Keep the same popup entry point and orchestration in `NotificationPanelVM`.
2. Replace selection model with per-row action state.
3. Extend `INotificationActionService.ExecuteChecklistActionAsync` to accept structured row decisions.
4. Reuse current money mutation and log/memory message patterns used in existing transaction flows.

Why this approach:
- Lowest risk and smallest integration surface.
- Preserves current user flow and dialog entry points.
- Avoids broad architecture churn while still enabling real processing behavior.

## Architecture
`Review Notifications` becomes an action matrix instead of a selected-items checklist.

- Each row has one selected action: `Ignore`, `Paid`, or `Process`.
- `Ignore` means no write for that row.
- `Paid` clears persisted matching notifications for that row.
- `Process` executes domain-specific logic then clears persisted matching notifications.

Category-specific `Process` behavior:
- `Upcoming Payment` and `Late Payment`: execute actual payment updates using the configured deduct source, update both source and credit/BNPL account balances/spent amounts, create logs/history actions, and mark matched notifications as cleared.
- `Fixed Expense Due`: execute expense processing using user-selected source from row `ComboBox`, apply corresponding account updates and logs/history actions, and clear matched notifications.

## Component Design

### 1) New action enum
Add `NotificationChecklistItemActionType`:
- `Ignore = 0`
- `Paid = 1`
- `Process = 2`

### 2) `NotificationChecklistActionItemVM`
Replace checkbox semantics with action semantics.

Proposed fields:
- `EntityId` (existing)
- `Label` (existing)
- `SelectedAction: NotificationChecklistItemActionType`
- `SelectedSourceId: int?` (used for fixed-expense `Process`)
- `AvailableSources: IReadOnlyList<SpendingSourceVM>` (for fixed-expense rows)
- `RequiresSourceSelection: bool` (true for fixed-expense rows)
- Convenience booleans for segmented toggle binding:
  - `IsIgnoreSelected`
  - `IsPaidSelected`
  - `IsProcessSelected`

Defaults:
- `SelectedAction = Ignore`
- `SelectedSourceId` defaults to the expense's current spending source if enabled; otherwise first eligible enabled source.

### 3) `NotificationChecklistActionVM`
Replace selected-item projection with actionable projections.

Proposed computed members:
- `PaidItems`
- `ProcessItems`
- `ActionableItems` (union of non-ignore rows)
- `CanProceed`: true when any row is non-ignore

`ProceedCommand` remains the popup confirmation action.

### 4) Popup XAML
For each row:
- Left: `TextBlock` for label
- Right: segmented selector with 3 options (`Ignore`, `Paid`, `Process`) visually matching existing segmented/view-mode style
- Conditional `ComboBox` for fixed-expense rows when `Process` is selected

### 5) Service contract
Change checklist action execution input from `selectedIds` to structured row decisions (entity id + action + optional source id + category context as needed).

## Data Flow
1. User opens action popup from grouped notification card.
2. `NotificationPanelVM` builds deduplicated row models by entity id.
3. Rows initialize to `Ignore`.
4. User selects row actions; fixed-expense rows can also select a source when `Process`.
5. On `Proceed`, VM sends only non-ignore rows to `INotificationActionService`.
6. Service processes rows in one operation scope:
   - `Paid`: clear matched notification records only.
   - `Process`:
     - Payment categories: execute payment mutation + logging + clear notifications.
     - Fixed-expense category: execute expense mutation using chosen source + logging + clear notifications.
7. Save once; caller refreshes notifications and dashboard data.

## Error Handling and Validation
- Fixed-expense `Process` requires a valid source selection.
- Missing/deleted entities (expense/source/notification) are treated as per-row no-op failures without corrupting other rows.
- Invalid payment prerequisites (missing deduct source, disabled account, etc.) produce row-level failure.
- If no rows produce mutations, return false and skip refresh side effects.
- All persisted mutations happen within a single operation scope for atomic save behavior.

## UX Rules
- `Ignore` is default on every row.
- `Ignore` rows are not persisted and not cleared.
- `Paid` and `Process` rows are both actionable and enable `Proceed`.
- Fixed-expense row source `ComboBox` appears only when action is `Process`.

## Testing Strategy

### ViewModel tests
- Rows default to `Ignore`.
- `CanProceed` only true when at least one row is `Paid` or `Process`.
- Fixed-expense source picker visibility toggles only for `Process`.

### Service tests
- `Paid` clears notifications and does not change account balances/spent amounts.
- `Process` for upcoming/late payment updates both accounts correctly and clears notifications.
- `Process` for fixed expense uses selected source and clears notifications.
- Mixed rows (`Ignore` + `Paid` + `Process`) execute only actionable rows.
- Missing/invalid row dependencies do not apply unintended changes.

## Risks and Mitigations
- Risk: duplicated money mutation logic.
  - Mitigation: extract small internal helper(s) shared by payment processing paths.
- Risk: row-level partial intent ambiguity.
  - Mitigation: explicit result accounting in service and deterministic per-row filtering.
- Risk: UX confusion around `Paid` vs `Process`.
  - Mitigation: keep labels explicit and add short helper text under popup title if needed.

## Implementation Boundaries
Primary files likely impacted:
- `Fluxo/Views/Popups/NotificationChecklistActionPopup.xaml`
- `Fluxo/ViewModels/Popups/NotificationChecklistActionVM.cs`
- `Fluxo/ViewModels/Popups/NotificationChecklistActionItemVM.cs`
- `Fluxo/ViewModels/Shell/Main/NotificationPanelVM.cs`
- `Fluxo/Services/Notifications/INotificationActionService.cs`
- `Fluxo/Services/Notifications/NotificationActionService.cs`

This design is intentionally incremental and constrained to current notification action architecture.
