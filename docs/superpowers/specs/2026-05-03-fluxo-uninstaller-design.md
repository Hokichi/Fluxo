# Fluxo Uninstaller Design

## Context
Fluxo currently ships an install-only Burn bundle with a custom WPF bootstrapper application (`Fluxo.Installer`). The UI flow currently includes `Welcome -> Progress -> Finished` and does not support a dedicated uninstall UX path launched from the installed app directory.

The requested behavior is to install a dedicated executable named `fluxo Uninstaller.exe` into the install directory. Running that executable must launch the same WPF window, skip welcome, show a dedicated uninstall page, execute uninstall, then transition to the existing finished page with uninstall-specific copy.

## Goals
1. Install `fluxo Uninstaller.exe` into `INSTALLFOLDER` during installation.
2. Launching `fluxo Uninstaller.exe` starts the same bootstrapper UI in uninstall mode.
3. Uninstall mode skips welcome and starts directly on a new `UninstallPage`.
4. `UninstallPage` displays:
   - Header: `Uninstalling Fluxo`
   - Sub-header: `It was a good ride with you`
   - `FluxoWave` visual
5. After uninstall success, show `Finished` page with:
   - Main title: `fluxo`
   - Subtitle: `Thank you for letting fluxo help`
   - Text under button: `Uninstallation complete.`
   - Close button available
6. Launch button remains hidden for uninstall outcomes.

## Non-Goals
1. Building a second standalone bootstrapper codebase for uninstall.
2. Adding uninstall triggers into the install welcome screen.
3. Changing existing install success copy.

## Architecture Overview

### 1) Packaging: Install `fluxo Uninstaller.exe`
- Add MSI authoring that installs `fluxo Uninstaller.exe` into `INSTALLFOLDER`.
- Source the file from the built bundle output executable so the uninstaller and installer stay version-aligned.
- Keep it managed by MSI so upgrades/repairs maintain the file.

### 2) Bootstrapper Mode Detection
- Detect startup mode from current process executable name.
- If executable name equals `fluxo Uninstaller.exe` (case-insensitive), set operation mode to `Uninstall`; otherwise default to `Install`.
- In uninstall mode:
  - initialize VM/UI on uninstall screen,
  - skip welcome,
  - on detect completion plan `LaunchAction.Uninstall`.

### 3) UI Flow
- Install mode flow remains unchanged.
- Uninstall mode flow:
  1. Open main window
  2. Show `UninstallPage`
  3. Execute Burn flow (`Detect -> Plan(Uninstall) -> Apply`)
  4. Transition to finished screen on completion

### 4) ViewModel and State Model
- Introduce an operation mode concept (`Install`, `Uninstall`) in the view model.
- Add an `Uninstall` entry to `InstallerScreen` so page switching supports the new page.
- Keep existing terminal states, but map finished copy by outcome + operation mode.

### 5) Finished Copy Mapping
- Install success/up-to-date and failure/cancel mappings remain as currently implemented.
- Uninstall success mapping:
  - `FinishedTitle`: `fluxo`
  - `FinishedSubtitle`: `Thank you for letting fluxo help`
  - finished status text under buttons: `Uninstallation complete.`
- Launch action must not be available for uninstall completion.

## Data Flow
1. User runs `fluxo Uninstaller.exe` from install folder.
2. Bootstrapper detects uninstall mode from executable name.
3. VM initializes with `Screen = Uninstall` and operation mode `Uninstall`.
4. Engine detect runs.
5. Detect complete triggers `Plan(LaunchAction.Uninstall, scope)`.
6. Apply completes:
   - success -> finished uninstall copy
   - failure -> finished failure path with status message

## Error Handling
1. If detect/plan/apply fails in uninstall mode, transition to finished failure state with explicit status text.
2. If uninstall launched when no installed product is detected, treat as no-op failure path and show finished failure state.
3. Cancellation before finished remains guarded by existing confirmation flow.

## Testing Strategy
Add/update tests in `Fluxo.Tests/Installer`:
1. Uninstall mode initialization skips welcome and sets uninstall screen.
2. Detect complete in uninstall mode requests uninstall planning.
3. Successful uninstall transitions to finished with exact requested copy.
4. Launch command is disabled for uninstall completion.
5. Existing install mode tests continue passing.

## Impacted Files
1. `Fluxo.Installer/BootstrapperEntry.cs`
2. `Fluxo.Installer/ViewModels/InstallerViewModel.cs`
3. `Fluxo.Installer/Models/InstallerScreen.cs`
4. `Fluxo.Installer/Views/MainWindow.xaml`
5. `Fluxo.Installer/Views/Pages/UninstallPage.xaml` (new)
6. `Fluxo.Installer/Views/Pages/UninstallPage.xaml.cs` (new)
7. `Fluxo.Installer.Msi/*.wxs` (component for `fluxo Uninstaller.exe`)
8. `Fluxo.Tests/Installer/*` (targeted VM/flow tests)

## Acceptance Criteria
1. `fluxo Uninstaller.exe` is installed into `INSTALLFOLDER` by MSI.
2. Running `fluxo Uninstaller.exe` opens the existing installer window and skips welcome.
3. `UninstallPage` displays required header, sub-header, and `FluxoWave`.
4. Uninstall operation is executed (not install) from that path.
5. Finished page shows:
   - `fluxo`
   - `Thank you for letting fluxo help`
   - `Uninstallation complete.`
6. Close button is available on finished page.
7. Launch button is hidden/disabled for uninstall outcome.