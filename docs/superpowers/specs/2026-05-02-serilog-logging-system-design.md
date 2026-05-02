# Serilog Logging System Design (Fluxo)

Date: 2026-05-02
Scope: Add robust application logging using Serilog, wire existing and new necessary try-catch blocks, and use Fluxo in-app username for daily log filenames.

## Goals

- Add centralized structured logging to Fluxo with Serilog.
- Persist logs in daily files named `<Username><MMDDYYYY>.log`.
- Ensure `Username` comes from Fluxo app settings (`PreferredDisplayName`), not OS username.
- Log meaningful failures from existing try-catch blocks while skipping expected non-error control paths.
- Add targeted new try-catch guards where unhandled failures can break user workflows.

## Non-Goals

- Logging every minor parse/probe fallback where exceptions are expected and intentionally ignored.
- Refactoring unrelated business logic.
- Changing user-facing error behavior unless required for safe logging.

## Key Decisions

1. Logger bootstrap and lifecycle
- Serilog is configured during app startup before critical initialization work.
- Logger is flushed and closed on app exit.
- Global handlers are attached for:
  - UI dispatcher unhandled exceptions
  - non-UI unhandled exceptions
  - unobserved task exceptions

2. Log file naming and location
- Directory: `Logs` under Fluxo app base directory.
- Filename format: `<SanitizedFluxoUsername><MMDDYYYY>.log`.
- Username source: app setting `UserSettingNames.PreferredDisplayName`.
- Username fallback: `User` if null/empty/whitespace.
- Sanitization: strip invalid filename characters and trim; fallback to `User` if result becomes empty.

3. Message strategy for catch blocks
- If operation context is known, use specific message with process/action details.
- If context is generic or weak, use:
  - `Failed to <performed process>. Please refer to <filename> for the issue's detailed`
- Existing user-facing dialogs/result messages remain intact unless mismatch creates ambiguity.

4. Necessary catch block policy
- Log catches that represent runtime failures likely impacting startup, data flow, wizard/setup, settings operations, and primary UI actions.
- Skip logging for expected/benign catches such as:
  - cancellation-driven control flow (`OperationCanceledException` where cancellation is expected)
  - converter parse-probe catches used as fallback branches

## Architecture

## Components

1. Logging bootstrapper
- New service/static helper to:
  - resolve Fluxo username from persistent settings
  - build daily file path
  - configure `Log.Logger`
  - provide safe fallback logger initialization

2. Exception logging helper
- Shared helper for consistent exception logging call sites:
  - contextual message overload
  - generic `Failed to ...` template overload
  - optional file-name parameter interpolation for user-aligned phrasing

3. App integration points
- `App.xaml.cs` startup and shutdown lifecycle integrates logger init/close.
- Global exception events route into logger.

4. Catch block integration
- Update existing high-value catches in app lifecycle, main window actions, wizard, settings, popup operations, and service fallback paths.
- Add new targeted try-catch blocks around uncovered failure-prone operations where needed.

## Data Flow

1. Startup
- App bootstraps minimal dependencies.
- Logging bootstrapper reads Fluxo preferred display name from settings store.
- Logger initialized with computed filename for current day.

2. Runtime
- Explicit catches log failures with contextual metadata.
- Unhandled exceptions are captured by global handlers and written to the same daily file.

3. Day boundary
- New app launch on another date writes to new filename due to date component in file name.

## Error Handling Plan

- Logger initialization failures should not crash app startup; fallback to `UserMMDDYYYY.log` and/or safe no-op behavior while preserving app boot path.
- Catch logging must not replace existing user feedback behavior (dialogs, result objects).
- In global unhandled handlers, log and preserve existing crash/shutdown semantics.

## Files Expected to Change

- `Fluxo/App.xaml.cs`
- `Fluxo/Extensions/ServiceCollectionExtensions.cs` (if DI helper wiring is required)
- New logging support files under `Fluxo/Services` or `Fluxo/Infrastructure` (final placement follows existing project conventions)
- Existing viewmodel/view/service files containing necessary catch blocks

## Testing and Verification

1. Build verification
- `dotnet build` solution

2. Functional checks
- Trigger representative failure flows in:
  - startup operations
  - settings apply/reset/delete
  - quick setup wizard actions
  - main window undo/redo and analytics open
- Confirm corresponding entries appear in expected log file.

3. Filename verification
- Confirm generated file name uses Fluxo preferred display name and current date (`MMDDYYYY`).
- Confirm sanitization behavior when display name includes invalid path chars.

4. Skip-path verification
- Confirm expected cancellation and converter parse fallbacks do not introduce noisy logs.

## Risks and Mitigations

- Risk: noisy logs from low-value catches.
  - Mitigation: strict necessary-catch policy and explicit skip list.
- Risk: filename instability when user renames preferred display name mid-day.
  - Mitigation: logger file target is resolved at startup for the running session; next app session picks new username.
- Risk: logging code throws during exception handling.
  - Mitigation: guard logger helper calls to avoid cascading failures.

## Implementation Acceptance Criteria

- Serilog is active and writes logs to `Logs/<FluxoUsername><MMDDYYYY>.log`.
- Username for filename comes from Fluxo preferred display name setting.
- Necessary existing catch blocks are logging failures with contextual or generic template message policy.
- New targeted try-catch blocks are added where needed for resilience.
- Build succeeds.
