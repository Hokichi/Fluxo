# Lowercase Fluxo Executable Design

Date: 2026-05-09

## Problem

Running the local Release installer bundle fails at the final verification step with:

`Verification failed: fluxo.exe was not found.`

The bootstrapper verifies `fluxo.exe`, but the application project currently emits `Fluxo.exe`. The built MSI inspected from the local Release bundle also contains no `.exe` file rows, so the installer package is not reliably installing the executable that verification expects.

## Goal

Make `fluxo.exe` the canonical application executable name from build output through installation and verification.

## Non-Goals

- Do not change installer branding, product name, or install folder.
- Do not rename the project directory or solution structure.
- Do not introduce a packaging-only alias that hides a differently named build output.
- Do not alter unrelated uninstall cleanup work already present in the working tree.

## Approach

The app project should emit the lowercase executable directly. The installer should continue verifying `fluxo.exe`, and the MSI should harvest the root executable from the app output directory.

This keeps a single canonical executable name across development output, MSI contents, installed files, and bootstrapper verification.

## Required Changes

1. Update `Fluxo/Fluxo.csproj` so the app build output executable is `fluxo.exe`.
2. Keep installer constants that already expect `fluxo.exe` unchanged unless tests reveal inconsistent casing elsewhere.
3. Fix MSI file harvesting if the current WiX `<Files Include="...\**">` pattern continues to omit root-level files from `FluxoAppOutputDir`.
4. Update tests and references that assert `Fluxo.exe` only where they represent the application output name.
5. Keep Docker or other runtime entrypoints aligned with the lowercase executable if they depend on the app output name.

## Verification

- Build `Fluxo.Installer.Bundle` in Release configuration.
- Inspect the generated local Release MSI file table and confirm it includes an executable row for `fluxo.exe`.
- Run the relevant installer unit tests.
- If practical, run the local Release bundle and confirm installation reaches the success state instead of final verification failure.

## Build-Lock Handling

If a build fails because `fluxo.exe` or another generated executable is running, stop and ask before terminating the process. If termination is approved, terminate only the blocking process and rebuild.

## Risks

- Changing the assembly/output name may affect references that assume `Fluxo.exe` or `Fluxo` as the executable file name.
- WiX harvesting may still need an explicit root-file include even after the app emits lowercase output.
- Existing uncommitted installer changes must not be overwritten while applying this fix.
