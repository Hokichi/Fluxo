# Startup Tray Grouped Notification Popup Design

Date: 2026-04-30  
Status: Draft for review

## Objective

Add a custom startup tray popup that summarizes notifications when Fluxo is launched from Windows autostart (`--startup-tray`) and only on that launch path.

## Scope

- Show startup popup only when app is launched with `--startup-tray`.
- Never show this popup for manual minimize-to-tray or close-to-tray later in the same session.
- Use grouped notification cards (not raw notification rows) as summary input.
- Keep existing tray menu interactions unchanged.

## Trigger Rules

1. App starts and resolves launch mode in `App.OnStartup`.
2. If launch mode is not tray autostart, skip startup popup entirely.
3. If launch mode is tray autostart:
   - Initialize tray icon and hide main window as today.
   - Attempt to build grouped notification startup summary once.
   - If summary is available and has groups, show popup exactly once.
4. No re-show in the same process lifetime.

## Message Rules

### Group count = 1

Build contextual sentence from the single group category:

- Fixed expense due group:
  - Count > 1 in group: `There are <x> fixed expenses due`
  - Count = 1 in group: `<expense name> is due`
- Upcoming payment group:
  - Count > 1 in group: `There are <x> credit cards due`
  - Count = 1 in group: `<card/source name> is due`
- Goal deadline group:
  - Count > 1 in group: `There are <x> goals reaching their deadlines`
  - Count = 1 in group: `Goal <name> is reaching its deadline`
- Late payment group:
  - Count > 1 in group: `There are <x> late payments due`
  - Count = 1 in group: `There is one late payment due`
- Default known category fallback:
  - Count > 1 in group: `There are <x> notifications`
  - Count = 1 in group: show the same header text as the single notification/group card

### Group count > 1

- Message is always: `There are <x> notifications`
- Here `<x>` is **group count** (as requested), not raw notification row count.

### Error/exception fallback

- If summary generation hits an internal exception or invalid state, show nothing.
- Do not show partial or fallback error text in popup.

## Popup UI and Behavior

- New dedicated popup window: `StartupNotificationPopup` (separate from tray menu popup).
- Popup appears near tray cursor/work area using existing tray popup positioning conventions.
- Content:
  - Summary text on the left.
  - Two circular buttons on the right:
    - `AngleRight` icon button: restore/open main app window.
    - `Close` button: dismiss popup only.
- Auto-close after 5 seconds if no interaction.
- Deactivation also closes popup.

## Interaction Contracts

- `AngleRight` action:
  - Hides popup.
  - Calls existing restore flow (`RestoreMainWindowFromTray`).
- `Close` action:
  - Hides popup only.
  - No side effects on notification state, tray state, or app process.

## Architecture

### New startup summary service

Add a focused service (for example `IStartupNotificationSummaryService`) responsible for:

- Loading persisted active notifications.
- Mapping to `NotificationVM` as needed.
- Grouping via existing `INotificationGroupingService`.
- Returning a compact startup summary DTO:
  - `GroupCount`
  - `PrimaryGroupCategory`
  - `PrimaryGroupItemCount`
  - `PrimaryHeader`
  - `PrimaryEntityName` (for single-item fixed expense/upcoming payment wording)
  - `PrimaryGoalName` (optional, single goal-deadline message case)
  - `Message`

This keeps `App.xaml.cs` orchestration thin and testable.

### App startup integration

In tray autostart branch of `App.OnStartup`:

1. Create tray resources and hide main window.
2. Ask summary service for startup summary.
3. If summary is valid and non-empty, create/show `StartupNotificationPopup`.
4. Wire popup actions to existing restore flow.

## Error Handling

- Any exception during summary building:
  - Catch locally.
  - Skip popup display.
  - Keep app startup path stable.
- Any popup creation/display exception:
  - Skip popup display.
  - Keep tray-only startup intact.

## Testing Strategy

### Unit tests

- Summary formatter:
  - single group fixed expense (1 vs many)
  - single group upcoming payment (1 vs many)
  - single group goal deadline (1 vs many, including name for singular)
  - multi-group message uses group count
- Error behavior:
  - summary provider exception returns no popup payload

### App/tray behavior tests

- `--startup-tray` launch attempts startup popup once.
- Non-startup-tray launch never attempts popup.
- Manual hide-to-tray path does not trigger startup popup.
- `AngleRight` action restores window.
- `Close` action dismisses popup only.
- Auto-close hides popup after 5 seconds.

### Manual QA

1. Launch with `--startup-tray` and one group -> contextual message appears.
2. Launch with `--startup-tray` and multiple groups -> `There are <groups> notifications`.
3. Launch with no active groups -> no popup.
4. Verify popup auto-closes at 5 seconds.
5. Verify `AngleRight` opens app.
6. Verify `Close` only dismisses popup.
7. Verify normal app launch and manual tray flows do not show startup popup.

## Scope Guardrails

- No changes to existing tray menu options.
- No changes to notification persistence policy.
- No changes to dashboard notification panel UX in this phase.
