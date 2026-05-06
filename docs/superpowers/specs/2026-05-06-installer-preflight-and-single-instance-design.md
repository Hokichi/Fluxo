# Installer Preflight and Single-Instance Design

Date: 2026-05-06
Project: Fluxo
Status: Draft for review

## 1. Scope and outcomes

This design covers three behavior changes:

1. Installer version gate must prevent re-install when installed version is the same or newer.
2. Running-process termination prompt must run at installer/repairer operation start for install, repair, and uninstall (not uninstall-only).
3. `Fluxo.exe` must enforce single-instance execution; launching again must activate the existing instance.

Out of scope:

- Installer visual redesign.
- New update server/channel logic.
- Multi-user session orchestration beyond current machine-level behavior.

## 2. Current behavior summary

- Version skip logic exists in `Fluxo.Installer/BootstrapperEntry.cs` (`OnDetectComplete` with `IsInstalledVersionSameOrHigher()`), but regression reports show latest-version installs still proceeding in practice.
- Termination confirmation is currently enforced only in uninstall flow via `CanProceedWithUninstall()` in `Fluxo.Installer/ViewModels/InstallerViewModel.cs`.
- Main app startup (`Fluxo/App.xaml.cs`) has no single-instance gate, so additional launches can start new processes.

## 3. Functional requirements

### 3.1 Installer operation preflight

- Before install, repair, or uninstall proceeds, detect running `fluxo` processes.
- If no running processes are found, continue.
- If running processes are found:
  - prompt user to terminate;
  - if user declines, stop operation and show finished-failed state with retry guidance;
  - if user accepts, terminate all discovered process IDs;
  - if any termination fails, stop operation and show finished-failed retry guidance.

Decision: if user declines termination, behavior is fail-and-stop (not retry in-place).

### 3.2 Version short-circuit

- On install mode only:
  - if detected installed version is equal to or higher than bundle version, stop before plan/apply and transition to `FinishedUpToDate`.
- Repair and uninstall must not be short-circuited by same/higher version detection.

### 3.3 Fluxo single instance

- On launch, only one `Fluxo.exe` instance may remain active.
- If an instance already exists, second launch must signal the active instance to restore + foreground its main window (including tray-hidden state), then exit.

Decision: activation behavior is always restore-and-foreground.

## 4. Proposed design

### 4.1 Installer preflight refactor

Create a shared preflight method in `InstallerViewModel`, for example:

- `EnsureFluxoCanBeStoppedForOperation(InstallerRequestedOperation operation)`

Responsibilities:

- Enumerate running process IDs via existing injectable `getRunningFluxoProcessIds`.
- If none, return success.
- Ask confirmation via existing injectable `requestTerminateRunningAppConfirmation`.
- If declined, transition to finished failed with operation-specific message.
- If accepted, terminate each ID via existing injectable `tryTerminateProcessById`.
- If any termination fails, transition to finished failed with operation-specific message.

Call sites:

- Install path (`Install()`) before prerequisites and detect.
- Maintenance repair path (`ContinueMaintenanceCommand` repair branch) before detect.
- Uninstall path (`StartUninstall()`) replacing uninstall-only `CanProceedWithUninstall()` usage.

Message policy:

- Use operation-specific copy:
  - installation did not run because fluxo is still open / could not be terminated;
  - repair did not run ...;
  - uninstallation did not run ...;
- Keep existing retry guidance pattern: close fluxo and run again.

### 4.2 Version short-circuit hardening

Retain short-circuit in `InstallerBootstrapperApplication.OnDetectComplete` and harden by ensuring:

- related installed versions are aggregated by max version (`_highestDetectedInstalledVersion`);
- short-circuit condition runs only when `GetOperationMode() == InstallerOperationMode.Install` and detect status is success;
- compare uses Burn `engine.CompareVersions` through existing `TryCompareVersions`.

No short-circuit in maintenance/uninstall flows.

### 4.3 Main app single-instance coordinator

Add a startup coordinator (new helper under `Fluxo/Infrastructure`) with:

- named mutex for primary-instance election;
- lightweight IPC activation signal channel (named pipe preferred).

Flow:

1. App startup attempts to acquire mutex.
2. If acquired:
- become primary;
- start background listener for activation signal;
- continue normal startup.
3. If not acquired:
- connect to named pipe and send `ACTIVATE`;
- exit immediately.

Primary activation handler (UI thread):

- call existing tray restore path (`RestoreMainWindowFromTray()`);
- force main window foreground (current window methods already include activation logic).

Failure behavior:

- if secondary cannot signal primary, log warning and exit (still prevents second active instance);
- if primary listener fails, log and continue running current instance.

## 5. Data and state transitions

### 5.1 Installer preflight transitions

- Start operation -> preflight
- Preflight pass -> continue detect/plan/apply pipeline
- Preflight decline -> `Screen=Finished`, `State=FinishedFailed`, status retry message
- Preflight terminate-fail -> same finished-failed transition

### 5.2 Install up-to-date transitions

- Detect begin collects current and installed versions
- Detect complete evaluates condition
- If same/newer in install mode -> `OnDetectedUpToDateVersion()` -> finished up-to-date
- Else -> normal planning/apply

### 5.3 Single-instance transitions

- Primary exists + second launch -> secondary sends `ACTIVATE` and exits
- Primary receives `ACTIVATE` -> restore + foreground

## 6. Testing strategy

### 6.1 Installer tests

Update/add tests in `Fluxo.Tests/Installer/InstallerFlowStateTests.cs`:

- install blocked when running+decline;
- repair blocked when running+decline;
- install/repair/uninstall blocked when termination fails;
- install/repair/uninstall proceed when termination succeeds.

Update/add bootstrapper-focused tests (new test class if required):

- install mode + same version => up-to-date transition and no plan/apply;
- install mode + higher installed version => up-to-date transition and no plan/apply;
- repair/uninstall + same/higher version => no up-to-date short-circuit.

### 6.2 App single-instance tests

Add unit/integration-friendly tests around coordinator abstractions:

- second launch path sends activation and exits;
- primary handler executes restore/foreground action upon activation signal.

## 7. Risks and mitigations

- Risk: process enumeration false negatives.
- Mitigation: preserve current default behavior when enumeration fails, but fail-safe on explicit terminate failures.

- Risk: activation IPC edge cases during early startup.
- Mitigation: initialize listener as early as possible after primary election; keep tray restore call idempotent.

- Risk: regression in maintenance mode detection.
- Mitigation: keep existing `InstallerOperationModeDetector` behavior and extend tests only around new preflight call points.

## 8. Implementation boundaries

Files expected to change in implementation phase:

- `Fluxo.Installer/ViewModels/InstallerViewModel.cs`
- `Fluxo.Installer/BootstrapperEntry.cs` (if hardening adjustments are needed)
- `Fluxo/App.xaml.cs`
- new helper under `Fluxo/Infrastructure` for single-instance coordination
- installer and app tests under `Fluxo.Tests`

No migration, schema, or installer authoring (`Bundle.wxs`) changes are expected for this scope.
