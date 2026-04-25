# MainWindow Analytics Open Gating And State Fade Design

Date: 2026-04-25
Status: Approved for planning
Owner: Codex

## Summary

Analytics should no longer appear immediately when opened from MainWindow. Every open request must show a single ToastPopup while Analytics is prepared (control creation, data load, and first render settle), and the drawer should animate in only after preparation completes. Analytics must be removed from the header hamburger menu. During MainWindow state transitions, Analytics visuals should fade consistently with the MainWindow content transition behavior.

## Goals

1. Show loading feedback every time Analytics is opened from MainWindow.
2. Prevent duplicate toasts while preserving existing AnalyticsVM feedback patterns where appropriate.
3. Remove Analytics from the hamburger menu and remove the associated click event path.
4. Keep Analytics layer and tab fading aligned with MainWindow state change behavior.

## Non-Goals

1. No redesign of Analytics layout or chart content.
2. No removal of non-hamburger open paths (drawer tab and Ctrl+A stay available unless separately requested).
3. No change to persistence or analytics query logic.

## Current Behavior

1. MainWindow.OpenAnalyticsPopup() immediately ensures control creation, applies range, and opens drawer animation.
2. Analytics loads on control Loaded via AnalyticsVM.LoadAsync().
3. AnalyticsVM uses ShowToastWhileAsync when dialog and settle dependencies are present.
4. Hamburger menu contains an Analytics button wired to OnAnalyticsButtonClick.
5. MainWindow state transitions fade ContentGrid, but Analytics layer/tab are not explicitly included in the same helper.

## Required Behavior

1. Every open request from MainWindow must gate drawer open until Analytics is ready.
2. Readiness includes:
   1. Analytics control created.
   2. Data refreshed for the current open range.
   3. UI settled and at least one render pass completed.
3. Exactly one toast should be visible for this open flow.
4. Hamburger Analytics entry and event hook must be removed.
5. Analytics visuals should fade away and return alongside MainWindow state transition behavior.

## Proposed Architecture

### 1) MainWindow-Owned Open Orchestration

1. Convert Analytics open entrypoints in MainWindow to async orchestration methods.
2. Add a guard flag (for example `_isPreparingAnalyticsOpen`) to suppress concurrent open attempts.
3. New flow for each open request:
   1. Ensure Analytics control exists.
   2. Apply bounded date range handoff.
   3. Show ToastPopup from MainWindow via IDialogService.ShowToastWhileAsync.
   4. Inside toast work, call Analytics preparation API with internal toast disabled.
   5. When preparation completes, call OpenAnalyticsDrawer().

### 2) Analytics Control Readiness API

1. Add a method on Analytics, for example `PrepareForOpenAsync(bool showInternalToast, CancellationToken cancellationToken = default)`.
2. This method always performs a refresh for open-time correctness, not only first load.
3. Keep existing loaded/unloaded lifecycle safe and idempotent.
4. Maintain internal state needed to avoid conflicting load calls.

### 3) AnalyticsVM Refresh Feedback Mode

1. Add explicit refresh API in AnalyticsVM that accepts feedback ownership, for example:
   - `RefreshForOpenAsync(bool showToast, CancellationToken cancellationToken)`
2. If `showToast` is false, run refresh + settle path without ShowToastWhileAsync.
3. Existing internal refresh triggers (date changes and debounced refreshes) can continue to use VM-owned feedback behavior.

### 4) Hamburger Menu Removal

1. Remove Analytics button block from MainWindow.xaml header popup section.
2. Remove `OnAnalyticsButtonClick` handler from MainWindow.xaml.cs.
3. Ensure no dangling event bindings remain.

### 5) State-Change Fade Alignment

1. Extend MainWindow fade helpers so state-change transitions include:
   1. ContentGrid
   2. AnalyticsDrawerLayer
   3. AnalyticsDrawerTabHost
2. Keep minimize/restore behavior consistent with current Window opacity behavior.
3. Keep reduce-motion handling unchanged for drawer translate/tab fades.

## Data Flow

### Open Analytics (Every Time)

1. User triggers open (drawer tab or Ctrl+A path routed through MainWindow open method).
2. MainWindow checks open/transition/preparation guards.
3. MainWindow shows toast and executes preparation work.
4. Analytics control refreshes data and waits for UI settle + render completion.
5. Toast closes.
6. Drawer opens.

### Close Then Reopen

1. User closes drawer.
2. Next open request repeats full preparation with MainWindow-owned toast.
3. Drawer opens only after preparation completes.

## Concurrency And Error Handling

1. Guard against re-entrancy while preparation is active.
2. Respect cancellation tokens for queued or superseded operations where feasible.
3. If preparation fails, propagate existing exception behavior from toast wrapper and do not open the drawer.
4. Ensure guard flags are reset in finally blocks.

## Testing Strategy

### Unit Tests

1. AnalyticsVM tests:
   1. New refresh API path with `showToast=false` still calls analytics service and toggles loading states.
   2. Existing toast path remains functional.
2. MainWindow shortcut/path tests (where practical) to confirm analytics shortcut still maps to open flow.

### Layout/Binding Tests

1. Add or update static XAML assertion test to verify Analytics entry is removed from MainWindow header popup XAML.

### Manual Verification

1. Open Analytics from drawer tab:
   1. Toast appears.
   2. Drawer opens only after load completes.
2. Close Analytics and reopen:
   1. Toast appears again.
   2. No duplicate nested toasts.
3. Use Ctrl+A:
   1. Same gated behavior as drawer tab.
4. Trigger maximize/restore repeatedly with Analytics visible:
   1. Analytics layer and tab fade with main content transition.
5. Confirm hamburger no longer shows Analytics.

## Risks And Mitigations

1. Risk: duplicated toasts if VM and MainWindow both own feedback.
   - Mitigation: explicit `showToast` parameter and MainWindow as single owner for open path.
2. Risk: perceived delay on every open.
   - Mitigation: toast gives immediate feedback and avoids showing half-loaded UI.
3. Risk: transition conflicts between drawer animation and window state animation.
   - Mitigation: preserve existing transition guards and sequence open after preparation.

## Rollout

1. Implement MainWindow orchestration and Analytics/VM APIs.
2. Remove hamburger Analytics path.
3. Add/update tests.
4. Validate manual checklist and keep behavior parity for non-target flows.
