# Fluxo Maintenance Flow Design

## Context
Fluxo ships a Burn bundle with a custom WPF bootstrapper application (`Fluxo.Installer`). The installed maintenance executable is the same bundle copied into the install directory, but it must not run the normal first-install welcome flow.

The requested behavior is to install a dedicated executable named `fluxo.Repairer.exe` into the install directory. Running that executable opens the existing WPF window at a maintenance choice page where the user can repair or uninstall Fluxo.

## Goals
1. Install `fluxo.Repairer.exe` into `INSTALLFOLDER` during installation and repair.
2. Launching `fluxo.Repairer.exe` starts maintenance mode and skips `InstallerWelcomePage`.
3. Maintenance mode starts on `InstallerAppFoundPage`.
4. `InstallerAppFoundPage` displays two radio choices:
   - `Repair`
   - `Uninstall`
5. `Repair` runs the Burn repair flow and overwrites installed app files from the bundle.
6. Repair must not overwrite or package any existing `.db` file.
7. `Uninstall` runs the Burn uninstall flow and removes the application, registry entries, and installed package state.
8. Uninstall success transitions to the existing finished page with uninstall-specific copy.

## Non-Goals
1. Building a second standalone bootstrapper codebase.
2. Keeping `fluxo Uninstaller.exe` as the installed maintenance executable name.
3. Changing the normal first-install welcome flow.

## Architecture Overview

### 1) Packaging
- Build app files into the MSI payload from `$(var.FluxoAppOutputDir)\**`.
- Exclude `$(var.FluxoAppOutputDir)\**\*.db` from harvested app files so repair does not overwrite user data.
- Copy the outer bundle executable into `INSTALLFOLDER` as `fluxo.Repairer.exe` after install or repair verification.
- If the source bundle path and destination repairer path are the same file, skip the copy to avoid self-overwrite lock failures.

### 2) Bootstrapper Mode Detection
- Detect startup mode from Burn's source-process path, original bundle source path, and current process path.
- If any executable name equals `fluxo.Repairer.exe`, or is a Fluxo `.exe` name containing `repair`, set operation mode to `Maintenance`; otherwise default to `Install`.
- The Burn source-process path matters because maintenance launches can report `WixBundleSourceProcessPath` as `fluxo.Repairer.exe` while `WixBundleOriginalSource` resolves to the registered installer path from the previous install.
- The current process path is only a fallback because the managed bootstrapper application can run from Burn's extraction cache as `Fluxo.Installer.exe`.

### 3) UI Flow
- Install mode flow remains unchanged.
- Maintenance mode flow:
  1. Open main window.
  2. Show `InstallerAppFoundPage`.
  3. User selects `Repair` or `Uninstall`.
  4. `Repair` executes `Detect -> Plan(Repair) -> Apply`.
  5. `Uninstall` executes `Detect -> Plan(Uninstall) -> Apply`.
  6. Transition to `FinishedPage` on completion.

### 4) ViewModel and State Model
- `InstallerOperationMode` distinguishes first install from maintenance launch.
- `InstallerRequestedOperation` tracks the selected Burn operation: `Install`, `Repair`, or `Uninstall`.
- `InstallerMaintenanceAction` tracks the radio selection on `InstallerAppFoundPage`.
- `InstallerScreen.AppFound` drives the new page in `MainWindow`.

### 5) Finished Copy Mapping
- Install success/up-to-date and failure/cancel mappings remain as currently implemented.
- Repair success uses the existing success page with `Repair complete.` status.
- Uninstall success mapping:
  - `FinishedTitle`: `fluxo`
  - `FinishedSubtitle`: `Thank you for letting fluxo help`
  - finished status text under buttons: `Uninstallation complete.`
- Launch action remains hidden for uninstall outcomes.

## Testing Strategy
Tests in `Fluxo.Tests/Installer` cover:
1. Repairer path detection uses the original bundle source path before the extracted process path.
2. Maintenance launch skips welcome and shows `InstallerAppFoundPage`.
3. Repair selection requests detect and plans `LaunchAction.Repair`.
4. Uninstall selection requests detect and plans `LaunchAction.Uninstall`.
5. Successful install/repair prepares `fluxo.Repairer.exe`.
6. Preparing `fluxo.Repairer.exe` skips self-copy when launched from the install directory.
7. MSI authoring excludes `.db` files from the app payload.

## Acceptance Criteria
1. Running `fluxo.Repairer.exe` opens the existing installer window at `InstallerAppFoundPage`.
2. `Repair` repairs installed files without overwriting `.db` files.
3. `Uninstall` removes the installed app through Burn uninstall.
4. The finished page shows the correct repair or uninstall result copy.
5. The bundle and installer projects build successfully.
6. Focused installer tests pass.
