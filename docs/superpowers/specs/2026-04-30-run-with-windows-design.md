# Run With Windows Design

Date: 2026-04-30
Status: Draft for review

## Objective

Implement a **Run with Windows** feature that:

- Adds a toggle in **Settings > Personalization**.
- Adds a matching toggle in **Startup Wizard > Preferences (Notifications step)**.
- Persists the value in `UserSettings` under a new key: `ShouldRunAtStartup`.
- Persists a close behavior preference under `UserSettings` for **When closing fluxo**.
- When enabled, starts Fluxo automatically with Windows and keeps it **tray-only** at launch.
- When disabled, removes autostart registration.

## User Experience

### Settings > Personalization

- Add a toggle row titled **Run with Windows** with a short description.
- Add a selection control directly under it:
  - Label: **When closing fluxo**
  - Options: **Minimize to tray** / **Exit**
- Toggle value participates in existing pending-changes behavior (Apply/Revert/Close prompt).
- Toggle default is **off** when the setting is missing.
- Close-behavior default is **Exit** when missing.

### Startup Wizard > Preferences

- Add the same **Run with Windows** toggle in the notification preferences step.
- Add the same **When closing fluxo** selection under the toggle on that step.
- Toggle default is **off** for first-time users (or when missing).
- Close-behavior default is **Exit** for first-time users (or when missing).
- Value is staged with other wizard step data and committed only when setup is completed.

### Startup Behavior

- If `ShouldRunAtStartup` is `true`, Fluxo registers itself in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with a startup argument: `--startup-tray`.
- Launches from Windows login use `--startup-tray` and start **hidden** with tray icon only.
- Manual launches (without `--startup-tray`) open normally.

### Tray Interaction

- **Single left-click** on tray icon opens Fluxo's custom tray popup.
- **Double left-click** opens Fluxo main window directly.
- Popup actions:
1. `Open fluxo` (brand typography matches existing `flux` + mint `o`)
2. `Check for updates` (placeholder, no behavior yet)
3. `Restart fluxo` (close current instance and relaunch in tray mode)
4. `Exit` (terminate app)

### Close Behavior

- If **When closing fluxo** is set to **Minimize to tray**:
  - Main window close action hides to tray instead of exiting.
  - Tray icon remains available to reopen or exit.
- If set to **Exit**:
  - Current close behavior remains: app exits.

### Tray Popup Visuals

- Popup is custom WPF UI (not default system context menu).
- Reuse app resources for consistency:
  - Colors from `Brush.Background.*`, `Brush.Text.*`, `Brush.Border.Subtle`.
  - Fonts from existing keys (`Bold`, `Medium`, etc.).
  - Hover/highlight matches ComboBox popup item behavior (`Brush.Background.Hover`).

## Technical Design

### Settings Storage

- Add `UserSettingNames.ShouldRunAtStartup`.
- Add `UserSettingNames.CloseBehavior`.
- Persist `ShouldRunAtStartup` as existing string bool pattern (`"True"` / `"False"`).
- Default parse fallback for `ShouldRunAtStartup`: `false`.
- Persist close behavior as string enum (`"MinimizeToTray"` or `"Exit"`), default `"Exit"`.

### Registration Service

- Add `IStartupRegistrationService` and Windows implementation.
- Responsibilities:
  - Write/remove Fluxo entry in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
  - Write command using current executable path and `--startup-tray`.
- Keep this service focused on OS registration only.

### Tray Host

- Add a tray host service/component responsible for:
  - Creating/disposal of `NotifyIcon`.
  - Handling single/double left-click.
  - Hosting and controlling the custom tray popup.
  - Dispatching popup commands (open, restart, exit, placeholder update action).

### App Startup Flow

- Extend `App.OnStartup` to detect both tray args:
  - `--startup-tray` (Windows autostart path)
  - `--startup--tray` (restart path requirement)
- Shared initialization remains intact (migration, first-run logic, main VM init).
- In startup-tray mode:
  - Instantiate `MainWindow` but do not show initially.
  - Set window hidden from taskbar (`ShowInTaskbar = false`) until explicitly opened.
  - Start tray host immediately.
- In normal mode:
  - Existing startup behavior unchanged.

### Open/Restore and Restart

- Open from tray:
  - Stop hidden mode, show window in taskbar, show window, activate.
- Restart from tray:
  - Launch new process with `--startup--tray` (tray-mode relaunch as requested).
  - Dispose tray host and shut down current process.

### Window Close Handling

- Read `CloseBehavior` from settings into shell startup/runtime state.
- On main window close request:
  - `MinimizeToTray` -> cancel close, hide window, keep process + tray alive.
  - `Exit` -> existing shutdown flow.

## Data Flow

1. User toggles `Run with Windows` in Settings or Startup Wizard.
2. User selects close behavior in `When closing fluxo`.
3. Values persist to `UserSettings.ShouldRunAtStartup` and `UserSettings.CloseBehavior`.
4. During apply/complete, app updates Windows Run registration to match value.
5. On next login launch:
  - Windows invokes Fluxo with `--startup-tray`.
  - Fluxo starts tray-only.

## Error Handling

- If startup registration update fails during apply:
  - Return failure in settings flow and show existing settings error UI.
- If tray host initialization fails in startup-tray mode:
  - Fallback to normal visible main window to avoid invisible app failure.
- If restart launch fails:
  - Keep current process alive and show error dialog.

## Testing Strategy

### Unit Tests

- `SettingsPersonalizationTabVM`:
  - Load default false for missing `ShouldRunAtStartup`.
  - Load default `Exit` for missing close behavior.
  - Pending-changes detection includes the new toggle.
  - Apply/Revert/Commit covers new toggle + close-behavior selection.
- `QuickSetupWizardNotificationVM`:
  - Loads/stages/saves `ShouldRunAtStartup` and close behavior.
- Startup registration service:
  - Produces expected Run entry command.
  - Removes entry when disabled.

### Startup/Behavior Tests

- Startup mode resolver for `--startup-tray` argument branch.
- Tray command routing:
  - open -> shows window
  - restart -> relaunch tray mode with `--startup--tray`
  - exit -> shutdown
- Close behavior:
  - `MinimizeToTray` -> close hides to tray
  - `Exit` -> close shuts down app

### Manual QA

1. Enable toggle in Settings, apply, verify Run key exists.
2. Disable toggle, apply, verify Run key removed.
3. Enable in Startup Wizard and complete setup, verify Run key exists.
4. Reboot/login: Fluxo starts tray-only.
5. Left-click tray icon: custom popup opens with four options.
6. Double-left-click tray icon: main window opens directly.
7. Restart action relaunches tray-mode instance with `--startup--tray`.
8. Exit action terminates Fluxo.

## Scope Notes

- `Check for updates` is intentionally unimplemented in this phase.
- No unrelated refactor is included.
- This design preserves existing startup migration, first-run checks, and settings conventions.
