# Tray Lifecycle Design (2026-05-01)

## Goal
Ensure Fluxo always boots with a tray icon once the main app window lifecycle begins. Keep close behavior setting-driven:
- `MinimizeToTray`: close hides to tray
- `Exit`: close fully terminates app and tray icon

## Scope
- In scope:
  - Startup tray initialization timing within `App.xaml.cs`
  - Preservation of close behavior routing in `MainWindow` + `App`
  - Test coverage updates for startup tray initialization behavior
- Out of scope:
  - Redesigning tray popup UX
  - Refactoring tray ownership into a new service
  - Changing first-run loader/wizard behavior

## Existing Behavior Summary
- Tray icon initializes during startup only when launch args indicate tray mode.
- On main window close, `App.TryHandleMainWindowClosingToTrayAsync` reads close behavior setting:
  - If `MinimizeToTray`, close is intercepted and window is hidden.
  - If not, close continues and application shuts down.
- `OnExit` disposes tray resources.

## Selected Approach
### Recommended option (selected)
Initialize tray icon unconditionally after `MainWindow` is created and `ShutdownMode` is set to `OnMainWindowClose`.

Why this option:
- Smallest safe change.
- Matches requirement: tray icon appears only when main window lifecycle begins.
- Keeps all existing shutdown/disposal and close behavior logic intact.

### Alternatives considered
1. `MainWindow`-driven tray initialization from view events.
- Rejected: adds view-to-app lifecycle coupling and timing risk.

2. New tray lifecycle service.
- Rejected for now: larger refactor than required.

## Detailed Design
### Startup lifecycle changes
Update `App.OnStartup`:
1. Complete loader/wizard flow as today.
2. Resolve `MainWindow`, assign `MainWindow`, set `ShutdownMode = OnMainWindowClose`.
3. Call `EnsureTrayIconInitialized()` unconditionally.
4. Branch existing launch behavior:
   - Tray launch mode: `HideMainWindowToTray(mainWindow)` and optional startup tray popup.
   - Normal launch mode: `mainWindow.Show()`.

### Close lifecycle behavior (unchanged in intent)
`MainWindow.OnWindowClosing` continues to call `App.TryHandleMainWindowClosingToTrayAsync(this)`.
- If close behavior is `MinimizeToTray`, cancellation + hide to tray.
- If close behavior is `Exit`, no interception, shutdown proceeds.

### Termination behavior
When shutdown proceeds (`Exit` case or tray-menu Exit), `OnExit` runs `DisposeTrayResources()` so both process and tray icon terminate together.

## Data Flow
1. App startup finishes dependency/bootstrap stages.
2. Main window lifecycle begins.
3. Tray icon is guaranteed initialized.
4. UI visibility depends on launch mode; tray presence is independent from launch mode.
5. On close request, setting determines minimize-to-tray vs full shutdown.

## Error Handling
- No new explicit error handling path is required.
- Existing startup catch remains the global fallback.
- Tray icon extraction already falls back to `SystemIcons.Application`.

## Test Plan
- Add/adjust tests to verify startup path initializes tray icon for normal launch.
- Keep existing behavior checks for:
  - close-to-tray when setting is `MinimizeToTray`
  - full shutdown when setting is `Exit`
- Run targeted and/or full test project to confirm no regressions.

## Risks and Mitigations
- Risk: startup sequence regression.
  - Mitigation: keep change localized to initialization point only.
- Risk: false assumption in testability around `NotifyIcon` creation.
  - Mitigation: test through observable behavior or existing abstraction seams.

## Implementation Notes
- Primary file: `Fluxo/App.xaml.cs`
- Likely test files: `Fluxo.Tests` startup/tray behavior tests

## Success Criteria
- On every startup where main window lifecycle begins, tray icon exists.
- If close behavior is `Exit`, closing app fully exits and no tray icon remains.
- If close behavior is `MinimizeToTray`, closing app hides to tray.
