# Add Current Amount for Savings in Add New Income Source

Date: 2026-04-29

## Goal
When creating or editing an income source in `Add New Income Source`, show `Current Amount` when `Source Type` is `Savings`, and persist that value as `Balance`.

## Scope
In scope:
- Form-field visibility logic for the `Current Amount` input.
- Reuse of existing amount binding so savings value writes to `Balance`.
- Minimal ViewModel and XAML updates only.

Out of scope:
- New persistence fields.
- Changes to credit/BNPL due-date, deduct-source, or account-limit behavior.
- UI restyling.

## Current Behavior
- `Current Amount` visibility is currently bound to `IsCashLike` (Checking/Cash only).
- `Savings` currently shows APY but not `Current Amount`.
- Save logic already maps non-credit-like `PrimaryAmountText` into `Balance`.

## Design

### 1) ViewModel visibility contract
Add a computed boolean in `AddSpendingSourceVM`:
- `IsBalanceLike` => `IsCashLike || IsSaving`

Update `OnSelectedSpendingSourceTypeChanged` to raise property-changed for `IsBalanceLike`.

Rationale:
- Keeps UI rule explicit and centralized in VM.
- Avoids duplicated XAML blocks or converter complexity.

### 2) XAML binding update
In `AddSpendingSourcePopup.xaml`, update the `Current Amount` section visibility binding:
- From `IsCashLike`
- To `IsBalanceLike`

Keep existing textbox binding unchanged:
- `Text="{Binding PrimaryAmountText, ...}"`

Rationale:
- Preserves existing value flow and validation behavior.
- Ensures savings enters amount through the same control path as checking/cash.

### 3) Data flow and persistence
No input schema changes.

Existing `TryBuildInput(...)` logic already maps:
- `Balance = IsCreditLike ? 0m : primaryAmount`

Since savings is not credit-like, `PrimaryAmountText` persists as `Balance` automatically.

## Error Handling and Validation
No new error states required.
Existing checks remain:
- Name required.
- Numeric values must be >= 0.
- Credit-specific rules unchanged.

## Testing Plan
- Add/update VM tests to verify:
  - `IsBalanceLike` is `true` for `Savings`, `Checking`, `Cash`.
  - `IsBalanceLike` is `false` for `Credit`, `BNPL`.
  - Source-type changes trigger correct derived-property updates.
- Existing save tests should continue passing because persistence mapping is unchanged.

## Rollout and Risk
Risk is low:
- Small, localized UI/VM change.
- No schema or repository changes.
- Existing save path reused.

Primary regression risk:
- Missing `OnPropertyChanged(nameof(IsBalanceLike))` could cause stale visibility after type switch.

Mitigation:
- Add VM test coverage for type toggling.
