# Header Search + Quick Add Design

Date: 2026-04-29  
Project: Fluxo  
Scope: Main window header actions and quick search migration

## Objective

Add two new header actions to the left of the hamburger menu:
- `Search` BalloonButton with `MagnifyingGlass`
- `Quick Add` BalloonButton with `PlusSolid`

Both use `Mint` as default background and `Mint.Muted` on hover.

Behavior requirements:
- Quick Add opens `Add New Transaction` directly.
- Search expands inline into a search box when clicked or when `Ctrl+F` is pressed.
- Search reuses current quick-search matching behavior.
- Selecting a search result collapses the expanded search UI.
- Search auto-collapses when focus leaves the search UI, when clicking outside, or when `Esc` is pressed.
- The standalone `QuickSearchPopup` is removed.

## Current Baseline

Current implementation uses:
- `Ctrl+F` in `MainWindow` to open `QuickSearchPopup` through `IDialogService.ShowQuickSearch(...)`.
- `QuickSearchPopup` hosts a textbox and results list that searches expense logs from `BudgetPanel.GetAllExpenseLogs()`.
- `Quick Add` is currently inside the header menu and opens `QuickAddPopup` (not direct add transaction).

## Recommended Approach

Implement inline header search directly in `MainWindow` (XAML + code-behind) and remove `QuickSearchPopup` entirely.

Why:
- Matches requested in-place expansion UX.
- Keeps shortcut handling in the same host that already owns keyboard shortcuts.
- Reuses existing result-to-expense-detail handoff logic (`OpenExpenseDetailPopup`) with minimal behavioral risk.

## Architecture and Component Changes

### 1) Main Window Header UI

Update `MainWindow.xaml` header right stack:
- Insert new `Search` `BalloonButton` left of `Quick Add`.
- Insert new `Quick Add` `BalloonButton` left of `HeaderMenuButton`.
- Keep existing hamburger/minimize/restore/close sequence otherwise unchanged.

Add an expandable search container in the same header area:
- Collapsed state: only Search BalloonButton visible.
- Expanded state: search textbox appears with search icon and results popup panel anchored to the header search area.

Visual styling:
- Both new BalloonButtons:
  - `DefaultBackground = Brush.Mint`
  - `HoveredBackground = Brush.Mint.Muted`
- Icons:
  - Search button: `MagnifyingGlass`
  - Quick Add button: `PlusSolid`

### 2) Main Window Behavior

Update `MainWindow.xaml.cs`:
- Replace `Ctrl+F` behavior from dialog launch to inline search expansion and focus.
- Keep `Ctrl+N` mapped to add transaction behavior.
- Add handlers/state for:
  - Expand search UI
  - Collapse search UI
  - Text changed filtering
  - Search result item click
  - Outside click + focus loss collapse
  - `Esc` collapse when search is expanded

Update Quick Add click handler:
- Header Quick Add button opens `OpenAddNewTransactionPopup()` directly.

### 3) Remove Obsolete Popup Surface

Delete:
- `Fluxo/Views/Popups/QuickSearchPopup.xaml`
- `Fluxo/Views/Popups/QuickSearchPopup.xaml.cs`

Remove references:
- `IDialogService.ShowQuickSearch(...)`
- `DialogService.ShowQuickSearch(...)`
- DI registration for `QuickSearchPopup` in `ServiceCollectionExtensions`
- Any remaining compile references/usings for `QuickSearchPopup`

## Data Flow

### Search Source
- Source remains the same logical dataset as previous quick search: `BudgetPanel.GetAllExpenseLogs()`.

### Search Rules (unchanged)
- Query is trimmed.
- If empty or length `<= 3`: hide results.
- Match where `Expense.Name` contains query (case-insensitive).
- Return first 5 matches.
- Show “No expense found” when no matches.

### Result Selection
- Clicking a result:
  - collapses the expanded inline search UI
  - opens `ExpenseDetailPopup` via `OpenExpenseDetailPopup(log)`

## Error Handling and Resilience

- Null-safe access for log/expense/name fields; logs with missing searchable names are excluded from matches.
- If search source is temporarily empty, UI remains functional and shows no results.
- If result click data context is invalid, ignore click without exception.

## Interaction and Accessibility Notes

- `Ctrl+F` always opens/focuses search when main window is active.
- `Esc` closes search only when expanded.
- Clicking outside the search region closes it.
- Auto-collapse on focus leaving the search UI to keep header uncluttered.

## Test and Verification Plan

### Functional
- Search button click expands search UI and focuses textbox.
- `Ctrl+F` expands/focuses search UI.
- Query with 4+ characters returns up to 5 matching expenses by name.
- Query 3 chars or fewer hides results.
- No-match query shows “No expense found”.
- Selecting a result collapses expanded search and opens expense detail popup for selected entry.

### Quick Add
- Quick Add button opens `Add New Transaction` popup directly.
- `Ctrl+N` continues to open add transaction flow.

### Collapse Behavior
- `Esc` collapses expanded search.
- Clicking outside collapses expanded search.
- Focus leaving search region collapses expanded search.

### Regression
- Hamburger menu behavior unchanged.
- Window chrome controls unchanged.
- Build compiles with `QuickSearchPopup` removed and no stale service references.

## Scope Boundaries

In scope:
- Header UI action additions.
- Inline search migration from popup.
- Shortcut rewiring for `Ctrl+F`.
- Removal of `QuickSearchPopup` and related plumbing.

Out of scope:
- Search ranking algorithm changes.
- Search across additional fields (source/tag/amount/date).
- Any new persistence, backend, or database changes.


