# Installer Runtime and Packaging Design

## Context

Fluxo is a WPF app targeting `net10.0-windows` with a custom WPF managed bootstrapper and WiX MSI/bundle packaging. The installer bundle should remain self-contained so setup can start on machines without .NET installed. The installed app should become framework-dependent and rely on the .NET 10 Windows Desktop Runtime x64 required by the app.

The current installer detects .NET availability, but it does not install the required runtime automatically. It also currently packages the app from a self-contained publish output, which leaves redundant runtime DLLs in the install directory and increases disk usage.

## Goals

- Keep the installer bootstrapper self-contained.
- Make the installed Fluxo app framework-dependent.
- Require the .NET 10 Windows Desktop Runtime x64, specifically the runtime that provides `Microsoft.WindowsDesktop.App`.
- Download and silently install the runtime from Microsoft release metadata when it is missing.
- Keep the full runtime detection, download, install, and verification sequence inside the existing `Installing` checklist step.
- Remove the downloaded runtime installer from `%TEMP%` when installation finishes or rolls back.
- Silently uninstall the .NET 10 Windows Desktop Runtime during Fluxo uninstall only when this Fluxo installer installed that runtime.
- Clean redundant files left by the previous `1.0.3` self-contained installation during the existing `Cleaning up` step.
- Preserve local user data, logs, repairer executable, and files required by the current build layout.

## Packaging Design

`Fluxo.Installer.Bundle` will continue publishing the managed bootstrapper as self-contained. Its payload generation remains responsible for embedding the bootstrapper runtime files into the bundle so the setup UI can start without machine-wide .NET.

`Fluxo.Installer.Msi` will stop publishing the app self-contained. Instead, it will build `Fluxo.csproj` for `net10.0-windows\win-x64` and harvest from the build output directory. The app output layout remains:

- root `fluxo.exe`
- root `fluxo.deps.json`
- root `fluxo.runtimeconfig.json`
- first-party DLLs under `libs`
- third-party managed DLLs under `vendor`
- native/runtime assets that the framework-dependent build still requires

Packaging tests that currently assert self-contained app publishing will be updated to assert framework-dependent app build output instead. Tests for the self-contained bootstrapper will remain.

## Runtime Install Flow

Runtime handling belongs entirely to the `Installing` step. The `Checking prerequisites` step will only validate local prerequisites such as the selected install folder.

When install or repair starts:

1. Mark `Checking prerequisites` as successful after local validation.
2. Mark `Installing` as running.
3. Detect whether `Microsoft.WindowsDesktop.App` major version `10` is installed for the x64 app requirement.
4. If present, continue to Burn detect, plan, and apply as today.
5. If missing, query `https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json`.
6. Select the `10.0` channel and follow its `releases.json`.
7. Select the latest Windows Desktop runtime executable with `rid` `win-x64`.
8. Download that executable into `%TEMP%`.
9. Run it silently and wait for the runtime installer process to finish.
10. Re-run runtime detection.
11. Continue with Fluxo installation only if the Windows Desktop Runtime x64 is now detected.

If runtime download, silent install, or post-install detection fails, the installer transitions to the existing failure and rollback handling. The UI status text may describe sub-work such as downloading or installing the runtime, but the active checklist step remains `Installing`.

Runtime acquisition failure is treated as an installation failure, not as a prerequisite-only failure. Even if Burn has not started applying the Fluxo MSI yet, the view model should enter the same post-start failure path used for app installation failures. That path should run the available app rollback callback and cleanup logic so any partially prepared app installation state is rolled back consistently.

When the runtime was missing before setup and this installer successfully installs it, the installer will persist a Fluxo-owned marker with the installed runtime version and architecture. The marker records ownership for cleanup decisions only; it does not claim ownership of runtimes that were already present before Fluxo setup started.

## Cancellation and Rollback

If the user cancels while runtime download or installation is active, the installer transitions to the existing `Rolling back` step.

Download cancellation behavior:

- Cancel or abandon the active download operation when the downloader supports safe cancellation.
- Delete the partially or fully downloaded runtime installer from `%TEMP%`.
- Continue the normal rollback completion path.

Runtime installation cancellation behavior:

- Terminate the runtime installer process only when the runtime acquisition service can determine that doing so is harmless, such as before the runtime installer has started making system changes.
- If termination is not known to be harmless, wait for the silent runtime installer process to finish instead of killing it mid-transaction.
- Run a silent uninstall for the runtime version installed by this setup attempt.
- Delete the downloaded runtime installer from `%TEMP%`.
- Continue the normal rollback completion path.

MSI rollback remains Burn's responsibility once Fluxo app installation has started. Runtime rollback is tracked separately because the runtime installer is launched by the managed bootstrapper rather than by the Burn chain.

## Runtime Uninstall Flow

During Fluxo uninstall, the installer will also evaluate whether it should remove the .NET 10 Windows Desktop Runtime x64.

The runtime uninstall flow is conservative because the Windows Desktop Runtime is a shared machine-wide dependency:

1. Run the normal Fluxo uninstall flow.
2. During cleanup, read the Fluxo runtime ownership marker.
3. If the marker is absent, skip runtime uninstall.
4. If the marker is present but does not match the .NET 10 Windows Desktop Runtime x64 installed by Fluxo, skip runtime uninstall.
5. If the marker matches, run the runtime silent uninstall command and wait for it to finish.
6. Re-run runtime detection to verify the marked runtime was removed or no longer satisfies the app-specific runtime condition.
7. Delete the Fluxo runtime ownership marker.

If runtime uninstall fails after Fluxo files have already been uninstalled, Fluxo uninstall should report cleanup failure instead of silently claiming complete success. The failure should not restore Fluxo files; it should leave the runtime installed and tell the user setup could not remove the dependency.

The installer must not silently uninstall a Windows Desktop Runtime that existed before Fluxo setup installed anything. That would risk breaking other desktop apps on the machine.

## Cleanup Design

After successful MSI apply for install or repair, the installer will move to `Cleaning up` before final verification. That step will run two cleanup operations:

1. Delete the downloaded runtime installer from `%TEMP%` if it exists.
2. Remove redundant files left by previous `1.0.3` self-contained installs.

The legacy cleanup scans only the selected install folder. It must preserve:

- `fluxo.db`
- log files and log directories
- `fluxo.Repairer.exe`
- the current root app files
- current `libs` files
- current `vendor` files
- unknown files that are not clearly old self-contained runtime artifacts

The cleanup should remove known redundant self-contained runtime artifacts that are no longer part of the current framework-dependent build. It should use the current build/install layout as an allowlist, plus a denylist of known runtime-pack files from the old self-contained output.

Cleanup failures should be handled conservatively. If a stale file cannot be deleted but does not block the new app layout, the installer can continue. If cleanup fails in a way that makes verification unreliable or leaves a conflicting app artifact, the install should fail and use the existing rollback flow.

## Components

- `DotNetRuntimeDetector`: update detection from `Microsoft.NETCore.App` to `Microsoft.WindowsDesktop.App` for major version `10`.
- Runtime acquisition service: query release metadata, resolve the Windows Desktop Runtime x64 installer URL, download to `%TEMP%`, run silent install, verify, delete temp file, and perform rollback uninstall when needed.
- Runtime ownership marker: persist enough information to know whether Fluxo installed the runtime and which runtime version/architecture may be removed during Fluxo uninstall.
- Runtime uninstall service: use the ownership marker to silently uninstall only the runtime installed by Fluxo, then verify and clear the marker.
- Installer view model: orchestrate runtime acquisition inside `Installing`, then continue existing Burn detect/plan/apply flow. It also triggers cleanup before final verification.
- Legacy install cleanup helper: scan the install folder and remove stale `1.0.3` self-contained runtime artifacts while preserving user data and current app files.
- Tests: cover runtime detection, metadata selection, cancellation/rollback, temp file deletion, packaging project assertions, and legacy cleanup preservation/removal rules.

## Verification

- Unit tests for `Microsoft.WindowsDesktop.App` detection from `dotnet --list-runtimes`.
- Unit tests for resolving the latest `10.0` Windows Desktop Runtime x64 URL from release metadata.
- Installer flow tests proving runtime download/install/recheck stays under `Installing`.
- Installer flow tests proving cancellation during runtime download or install uses `Rolling back`.
- Installer flow tests proving app uninstall removes the runtime only when Fluxo installed it.
- Installer flow tests proving app uninstall preserves pre-existing Windows Desktop Runtime installations.
- Cleanup tests proving database/logs/repairer/current build files are preserved.
- Cleanup tests proving known old self-contained runtime artifacts are removed in `Cleaning up`.
- Packaging tests proving the app MSI uses build output and the bootstrapper remains self-contained.
- A full local build of the solution and installer projects.
