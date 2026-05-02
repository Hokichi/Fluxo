# Installer Three-Screen Shell Design

## Summary
Refactor the installer UI from a single layout to a shell + three-page structure while preserving the existing installer command behavior and using shared `Fluxo.Resources` styles. The new flow is:

1. Welcome + Directory + Actions
2. Installation Progress Checklist
3. Final Actions

This design keeps one window and one view model, and splits visual content into three user controls for maintainability.

## Goals
- Match the visual direction of `StartupWizardPopup` in Fluxo.
- Reuse shared UI resources from `Fluxo.Resources` (no duplicated ad-hoc styles).
- Preserve existing install actions and command semantics where possible.
- Add explicit progress tracking and rollback visibility.
- Enforce rollback and installation-folder cleanup behavior on installation failure.

## Non-Goals
- Introducing WPF `Frame` navigation.
- Replacing WiX bootstrapper event sequencing.
- Refactoring installer logic into multiple view models.

## UI Architecture

### Shell
- Keep `Fluxo.Installer.Views.MainWindow` as the only window shell.
- Shell owns top-level popup-like chrome and active-page host.
- Shell switches content by screen state from `InstallerViewModel`.

### Pages
Create three user controls under `Fluxo.Installer/Views/Pages`:

1. `InstallerWelcomePage`
- Header and introductory copy.
- Install folder input.
- `Install` and `Change Directory` buttons.

2. `InstallerProgressPage`
- Checklist with step-by-step installation state.
- Current status line and failure details.

3. `InstallerFinishedPage`
- Header text: `Let's begin`
- Sub-text: `Your finance, simplified`
- Two icon actions:
  - Rocket button: launch Fluxo
  - Close icon button: close installer

## Style Mapping (must use existing shared resources)
- Install location `TextBox`: `RoundedTextInputStyle`
- Text buttons (`Install`, `Change Directory`): `PopupTextButtonStyle`
- Round icon buttons on final page: `WizardCircleActionButtonStyle`
- Card/surface structure and spacing: `PanelStyle`/theme brushes (`Brush.Background.*`, `Brush.Border.Subtle`, `Brush.Text.*`)
- Existing icon resources (`AngleRight`, `Close`, and app icon asset) should be consumed from shared dictionaries.

## State Model

### Screen State
Introduce explicit screen state in installer VM:
- `Welcome`
- `Progress`
- `FinishedSuccess`
- `FinishedFailed`

### Checklist Step State
Add step state model:
- `Pending`
- `Running`
- `Success`
- `Failed`

Checklist rows (fixed order):
1. `Prerequisites`
2. `Installing the app`
3. `Clean up (if exists)`
4. `Rollback (if failed)`

## Behavioral Flow

### Welcome Screen
- Default entry screen.
- `Install` command transitions to `Progress` and starts pipeline.
- `Change Directory` keeps existing folder-picker behavior.

### Progress Screen
Pipeline execution order:
1. `Prerequisites`
- Validate required runtime/environment prerequisites.
- If missing prerequisites:
  - Mark step as `Failed`.
  - Show message box listing missing components.
  - Stop pipeline and keep installer on progress/failure state.
  - Instruct user to install prerequisites and run installer again.

2. `Installing the app`
- Existing detect/plan/apply sequence.
- Mark `Success` only after apply succeeds.

3. `Clean up (if exists)`
- Execute post-install cleanup tasks when applicable.
- Mark `Success` when cleanup completes or no cleanup is needed.

4. Failure handling and rollback
- If any step after installation start fails:
  - Mark failing step as `Failed`.
  - Run rollback step.
  - Mark `Rollback (if failed)` as `Running` -> `Success`/`Failed`.

### Folder Deletion Rule
Track install-folder origin at install start:
- `InstallFolderExistedBeforeInstall`

On rollback path:
- If installer created the install folder in this run (folder did not exist before), attempt folder deletion after rollback.
- Do not delete pre-existing user folder.

### Final Screen
- `FinishedSuccess` routes to final page with launch and close icon actions.
- `FinishedFailed` remains terminal with failure messaging and rollback outcome visible.

## Commands
Preserve and reuse existing commands:
- `InstallCommand`
- `ChangeDirectoryCommand`
- `LaunchAppCommand`

Add:
- `CloseInstallerCommand` to close the shell (used by finished-page close icon).

## Error Messaging
- Prerequisite failure: modal message box with explicit missing components.
- Install failure: status messaging includes the failed step.
- Rollback failure: include both original installation error and rollback error.

## File-Level Plan
- Update `Fluxo.Installer/Models/InstallerState.cs` for screen states.
- Update `Fluxo.Installer/ViewModels/InstallerViewModel.cs`:
  - Add screen + checklist step states.
  - Add prerequisite check reporting.
  - Add rollback/folder-origin tracking and cleanup behavior.
  - Add close command.
- Refactor `Fluxo.Installer/Views/MainWindow.xaml` into shell host.
- Add:
  - `Fluxo.Installer/Views/Pages/InstallerWelcomePage.xaml`
  - `Fluxo.Installer/Views/Pages/InstallerProgressPage.xaml`
  - `Fluxo.Installer/Views/Pages/InstallerFinishedPage.xaml`
- Add code-behind only where required for shell close wiring.

## Verification Plan
1. Build `Fluxo.Installer`.
2. Validate success path:
- Welcome -> Progress -> FinishedSuccess
- Rocket launches Fluxo.
- Close icon closes installer.
3. Validate prerequisite failure path:
- Missing runtime triggers message box and stops flow.
4. Validate induced installation failure path:
- Rollback step runs.
- Install folder deletion follows rule (delete only if created by current run).
5. Validate shared style usage in XAML:
- `RoundedTextInputStyle`
- `PopupTextButtonStyle`
- `WizardCircleActionButtonStyle`

## Risks and Mitigations
- Risk: rollback/folder deletion can remove unintended paths.
- Mitigation: gate deletion behind strict `InstallFolderExistedBeforeInstall == false` and valid rooted path checks.

- Risk: style drift from Fluxo wizard.
- Mitigation: consume shared dictionaries and keys directly, avoid local color/style duplication.
