# Analytics Open-Time Range Handoff Design

Date: 2026-04-25
Status: Approved (conversation)

## Summary

When Analytics is opened from MainWindow, it should copy the current MainWindow date range once.

- If MainWindow is in Daily, Weekly, or Monthly mode: Analytics receives that resolved range.
- If MainWindow is in AllTime mode: Analytics range is not modified.
- Analytics retains its existing hard limits and behaviors (max 31-day range, warning text, debounce, trend presentation rules).
- Analytics does not remain synchronized with MainWindow after opening.

## Problem

Analytics is lazy-loaded in the drawer and currently loads with its own default range, so it can miss the currently selected dashboard period.

## Goals

- Open Analytics with the same bounded period as MainWindow when MainWindow is not AllTime.
- Preserve the current overload protections in AnalyticsVM.
- Keep behavior one-time on open, not live synchronization.

## Non-Goals

- No new all-time analytics mode.
- No removal or weakening of the 31-day clamp.
- No continuous two-way or one-way syncing after open.

## Decision

Use explicit open-time handoff in MainWindow before the first Analytics load.

### Behavior Matrix

- Daily mode: copy selected day range (from = to = selected day).
- Weekly mode: copy selected week range (Monday..Sunday for selected week).
- Monthly mode: copy selected month range (first day..last day of selected month).
- AllTime mode: do nothing; Analytics uses its own defaults (today..today on first open).

## Design

### 1) MainWindow resolves the current scope

MainWindow reads:

- `MainVM.ViewModeToggle.SelectedMainContentViewMode`
- `MainVM.DaySpinner.SelectedDay.Date`

Then:

- If mode is `AllTime`, return without touching AnalyticsVM dates.
- Otherwise, call `DateRangeResolver.Resolve(selectedDate, mode)` and assign to AnalyticsVM.

### 2) Apply before first load

MainWindow applies the resolved range into AnalyticsVM before opening the drawer content for the first time. This ensures the first `LoadAsync` runs against the copied range.

### 3) Keep existing constraints

No changes to:

- `AnalyticsVM` max-range clamp (31 days)
- Warning messaging
- Debounced refresh behavior
- Existing disposal/lifecycle logic

## Implementation Plan (Code-Level)

- Update `MainWindow.xaml.cs`:
  - Add helper method to apply bounded range to AnalyticsVM at open time.
  - Call helper from analytics-open flow after resolving/creating Analytics view and before opening drawer animation.
- No changes required to `AnalyticsVM` or `AnalyticsService` for this feature.

## Error Handling and Edge Cases

- If mode is AllTime, MainWindow intentionally does not set dates.
- If selected range exceeds 31 days (for example monthly in long months), AnalyticsVM existing logic clamps to 31 days and shows current warning.
- If Analytics drawer was already loaded, no re-copy occurs unless drawer lifecycle is recreated (current behavior remains).

## Testing Strategy

### Unit tests

Add tests around the new MainWindow range-application helper:

- Daily/Weekly/Monthly mode maps to expected date bounds assigned to AnalyticsVM.
- AllTime mode does not modify AnalyticsVM StartDate/EndDate.

### Regression checks

- Existing AnalyticsVM tests for 31-day clamp stay green.
- Manual check:
  - Select Week 16 in main view, open Analytics, verify it loads that week.
  - Switch to AllTime in main view, open Analytics, verify it loads default today..today.

## Risks

- MainWindow helper may run after first load if placed incorrectly. Mitigation: invoke before drawer open/load path completes.
- Monthly selected range may exceed 31 days and clamp unexpectedly. This is intended per current product decision.

## Acceptance Criteria

- Opening Analytics in Daily/Weekly/Monthly opens with the current MainWindow period (subject to existing 31-day clamp).
- Opening Analytics in AllTime does not alter Analytics range and loads its default today period.
- Changing MainWindow range after Analytics opens does not alter Analytics until reopened/recreated.
