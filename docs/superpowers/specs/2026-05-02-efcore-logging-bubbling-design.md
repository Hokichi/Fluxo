# EF Core Logging And Error Bubbling Design (Fluxo)

Date: 2026-05-02
Scope: Add EF Core logging, move shared logging service out of UI layer, and enforce sanitized process-specific UI messages for data failures.

## Goals

- Capture EF Core layer activity/failures in the same Serilog pipeline used by app logs.
- Move logging service implementation out of `Fluxo` (UI project) into reusable service-layer code.
- Bubble data failures upward as process-specific, sanitized messages:
  - `Failed to <perform process>. Please refer to <log filename> for the issue's detailed.`
- Keep full technical exception details in logs only.

## Constraints

- `Fluxo.Data` cannot reference `Fluxo.Services` because `Fluxo.Services` already references `Fluxo.Data`.
- Shared contracts required by all layers must live in `Fluxo.Core`.

## Architecture

### 1) Shared Logging Contract (Core)

Add in `Fluxo.Core`:
- `ILogService` interface for layer-agnostic logging operations and current log filename access.
- `DataOperationException` (custom exception) containing:
  - `PerformedProcess`
  - `UserMessage` (already formatted generic message)
  - Inner exception preserved.

This allows `Fluxo.Data`, `Fluxo.Services`, and `Fluxo` to share behavior without cyclic references.

### 2) Logging Service Implementation (Services)

Move/create concrete logger in `Fluxo.Services`:
- `SerilogLogService` (or equivalent) implementing `ILogService`.
- Maintains filename rules and generic message helper.
- Initializes and rotates log file using app username (`PreferredDisplayName`) and `<MMDDYYYY>`.

`Fluxo` project calls this service, but does not own implementation.

### 3) EF Core Logging Integration (Data)

In `Fluxo.Data.Extensions.ServiceCollectionExtensions`:
- switch `AddDbContext` to overload with `IServiceProvider`.
- resolve `ILogService` and wire EF logging via `optionsBuilder.LogTo(...)` to shared logger.
- log command/infrastructure/update failures; keep sensitive data logging disabled.

### 4) Data Exception Wrapping (Data/Services boundary)

Enhance `IDataOperationRunner` usage with process names.
- Add overloads: `RunAsync(string performedProcess, ...)`.
- In runner, for non-cancellation exceptions:
  - log full details (exception + process + operation context).
  - throw `DataOperationException` with sanitized `UserMessage` using required template.

### 5) UI Bubble Behavior (Fluxo)

Update existing UI catches that currently append raw `exception.Message`:
- if `DataOperationException`, display only `UserMessage` in dialog/result.
- keep existing non-data exception handling behavior where appropriate.

## Necessary Catch Policy

- Keep current skip rules for expected cancellation and converter parse-probe fallbacks.
- Apply bubbling behavior to operations that perform EF/database work (save/update/delete/load flows).

## File Plan

Create/Modify likely files:
- `Fluxo.Core/Interfaces/Services/ILogService.cs` (new)
- `Fluxo.Core/Exceptions/DataOperationException.cs` (new)
- `Fluxo.Core/Interfaces/Operations/IDataOperationRunner.cs` (modify)
- `Fluxo.Services/Logging/*` (new/moved implementation)
- `Fluxo.Services/Fluxo.Services.csproj` (Serilog references)
- `Fluxo.Data/Extensions/ServiceCollectionExtensions.cs` (EF LogTo wiring)
- `Fluxo.Data/Operations/DataOperationRunner.cs` (process-named wrapping)
- `Fluxo/App.xaml.cs` and selected `ViewModels/*` catches for sanitized message display
- remove old UI-owned logger implementation once replaced.

## Error Message Rules

- UI-safe only:
  - `Failed to <perform process>. Please refer to <log filename> for the issue's detailed.`
- No raw provider/SQL/stack text in user dialogs.
- Full details only in logs.

## Validation

- `dotnet build Fluxo.slnx` succeeds.
- Simulate data failure (e.g., forced DbUpdateException):
  - EF/exception details present in log file.
  - UI shows only sanitized process-specific message.
- Confirm logger implementation resides outside UI project and is used cross-layer.

## Risks

- Message regressions where old catches still append raw `exception.Message`.
- DI registration order for `ILogService` vs `IDataOperationRunner`/DbContext.

Mitigations:
- centralize creation of `DataOperationException` in runner.
- register `ILogService` before data operation services in app startup.
