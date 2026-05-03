# Installer Cancel/Rollback and Finished-State UX Design

## Context
The current installer supports welcome, progress, and finished screens with rollback handling on post-start failures. The close command currently exits immediately from any screen, and the finished page uses static copy (`Let's begin` / `Your finance, simplified`) regardless of failure or cancellation outcome.

This design adds explicit cancellation semantics and outcome-based finished UX while preserving existing install/apply/verify wiring.

## Goals
1. If the user attempts to close the installer before the final screen, show a confirmation dialog asking whether to cancel installation.
2. If the user confirms cancellation after install has started, execute rollback while showing rollback progress, then transition to finished.
3. For rollback scenarios (either failure-driven or cancellation-driven), replace the progress checklist with only `rollbackChecklistStep`.
4. On finished screen, show outcome-specific messaging:
   - Failed or missing prerequisites: `Installation failed`
   - User cancelled: `Installation cancelled`
   - Subtitle for non-success outcomes: `Please close the setup and run it again`
5. Remove duplicate close affordances by hiding the top-right main window close button on finished screen.
6. On finished screen, hide launch (rocket) action for non-success outcomes.

## Non-Goals
1. Changing Burn engine transaction behavior or rollback semantics.
2. Adding new pages/screens beyond the existing three-screen shell.
3. Changing success-path copy and behavior.

## Design Overview

### 1) Outcome Model
Introduce an explicit finished outcome in `InstallerViewModel` separate from low-level `InstallerState`:
- `Success`
- `Failed`
- `Cancelled`

The outcome drives:
- Finished title/subtitle text bindings
- Launch button visibility
- Whether close can happen directly vs guarded by cancel confirmation

### 2) Close Flow (Welcome/Progress Guard)
`CloseInstallerCommand` becomes outcome-aware:
- If `Screen == Finished`: close immediately.
- If `Screen != Finished`: show cancel-confirmation dialog.
- If user declines: no state change.
- If user confirms:
  - If install has started: run rollback and show rollback-only progress.
  - If install has not started: skip rollback execution.
  - Transition to finished with `Cancelled` outcome.

This ensures both Welcome and Progress close attempts are guarded, as requested.

### 3) Rollback Presentation Rule
For any rollback path (failure-triggered or user-cancel-triggered), mutate `ChecklistSteps` collection to contain only `rollbackChecklistStep` before rollback begins.

Behavioral notes:
- Rollback status transitions remain `Running -> Success/Failed`.
- Existing failure message details are preserved in `StatusMessage`.
- If rollback cannot run (callback unavailable/error), represent as failed rollback step and still transition to finished failed/cancelled outcome.

### 4) Failure And Prerequisite Mapping
- Post-start install/plan/apply/verify failures: finished outcome = `Failed`.
- Missing prerequisites / invalid install-folder precheck failures: finished outcome = `Failed` and transition to finished screen (instead of staying on welcome/progress state loop).
- Success path remains unchanged: finished outcome = `Success`.

### 5) Finished Screen Copy + Actions
Bind finished page text to VM properties:
- `Success`: `Let's begin` + `Your finance, simplified`
- `Failed`: `Installation failed` + `Please close the setup and run it again`
- `Cancelled`: `Installation cancelled` + `Please close the setup and run it again`

Action visibility:
- Launch/rocket visible only for `Success`.
- Finished-page close button remains available for all finished outcomes.

### 6) Top-Right Close Visibility
In `MainWindow.xaml`, hide header close button when `Screen == Finished` so only page-level close controls remain on finished page.

## Data Flow
1. User clicks close in header/welcome/progress.
2. VM requests confirmation through injectable dialog callback.
3. If confirmed, VM enters cancellation path:
   - switch to progress screen
   - replace checklist with rollback-only step
   - run rollback if needed
   - set outcome `Cancelled`
   - set state/screen to finished
4. UI bindings update finished copy + button visibility according to outcome.

## Error Handling
1. Rollback callback throws/returns false: show failed rollback step and keep failure details in status.
2. Cancellation requested before install start: no rollback call, direct `Cancelled` finished state.
3. Confirmation dialog unavailable (callback missing): safest default is no cancel (do not close silently).

## Testing Strategy
Update/add unit tests in `Fluxo.Tests/Installer`:
1. Close from welcome shows confirmation; decline keeps installer open.
2. Close from welcome confirm transitions to finished cancelled (no rollback call).
3. Close from progress confirm triggers rollback callback and rollback-only checklist.
4. Post-start failure rollback path uses rollback-only checklist.
5. Missing runtime transitions to finished failed outcome and failed copy properties.
6. Finished copy/action properties for success vs failed vs cancelled.
7. Launch command disabled for non-success outcomes.

## Impacted Files
1. `Fluxo.Installer/ViewModels/InstallerViewModel.cs`
2. `Fluxo.Installer/Views/Pages/InstallerFinishedPage.xaml`
3. `Fluxo.Installer/Views/MainWindow.xaml`
4. `Fluxo.Tests/Installer/InstallerFlowStateTests.cs`
5. Potentially new/updated installer-focused test file(s) for close/cancel behavior.

## Acceptance Criteria
1. Attempting to close before finished always prompts for cancellation confirmation.
2. Confirming close during/after start executes rollback flow and displays rollback progress.
3. Any rollback scenario shows only `rollbackChecklistStep` in progress checklist.
4. Finished page copy matches outcome mapping exactly.
5. Non-success finished outcome hides rocket action.
6. Top-right close is hidden on finished screen.
