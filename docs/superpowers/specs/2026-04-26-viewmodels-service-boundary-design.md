# ViewModels Service Boundary Refactor Design

Date: 2026-04-26  
Scope: `Fluxo/ViewModels/**`, `Fluxo.Core/Interfaces/Services/**`, `Fluxo.Services/**`, DI registrations, and related tests.

## Problem Statement

Several ViewModels and ViewModel-side helper modules currently depend on `IUnitOfWork` directly. This couples UI orchestration to persistence internals, duplicates transaction logic across ViewModels, and makes service responsibilities unclear.

## Goals

- Remove direct `IUnitOfWork` dependencies from all ViewModels and ViewModel-side helper modules.
- Ensure all persistence and transaction logic is accessed through services.
- Clean up existing services by moving workflow-heavy methods into dedicated services where needed.
- Remove superseded methods from old services instead of keeping compatibility shims.
- Keep existing user-visible behavior and messaging semantics stable.

## Non-Goals

- Re-architecting the entire data layer.
- Changing UI flows, copy, or view layout behavior.
- Replacing current messaging/event patterns unless required for data transfer.

## Design Overview

### Boundary Rule

After refactor:

- No type under `Fluxo/ViewModels/**` references `IUnitOfWork` in fields, constructors, properties, or method parameters.
- ViewModel helper/static modules (for example `SettingsShared`, `QuickSetupWizardShared`, `GoalUpdateTransactionSupport`) do not accept or use `IUnitOfWork`.
- Persistence logic resides in services only.

### Service Topology

Keep and narrow existing generic persistence services:

- `IExpenseService` / `ExpenseService`
- `IExpenseLogService` / `ExpenseLogService`
- `ISpendingSourceService` / `SpendingSourceService`
- `ITagService` / `TagService`
- `IAnalyticsService` / `AnalyticsService` (unchanged unless impacted by moved calls)

Add dedicated workflow services for multi-entity transactional workflows:

- `IQuickTransactionService`
  - Quick Add expense/income/goal transactions
  - Transfer funds transaction
  - Expense detail edit transaction
- `ISpendingSourceWorkflowService`
  - Add/edit spending source workflow (including balance update side effects)
  - Toggle visibility / enabled
  - Delete with activity guards
  - Deduct-source option loading helpers
- `IFixedExpenseWorkflowService`
  - Add/edit fixed expense workflow with tag resolution
  - Fixed expense settings batch actions
- `ISavingGoalWorkflowService`
  - Add/edit saving goal workflow
  - Saving goal settings batch actions
- `ISettingsService`
  - Settings load/upsert/id-set upsert operations
  - Reset-all-settings and delete-all-data maintenance flows
  - Spending source/settings batch operations currently executed in tab ViewModels
- `IQuickSetupWizardService`
  - Wizard load/apply operations currently spread across `QuickSetupWizard*` ViewModels/helpers
- `ITagPolicyService`
  - Resolve special tags (`Goal Update`, `Balance Update`, `Transfer`) and policy-based tag behaviors

## Service Cleanup and Method Relocation

### Principle

If an existing generic service method represents a workflow transaction across multiple aggregates, move it to a dedicated workflow service.

### Required Removals

Methods moved out of legacy service interfaces are removed from:

- service interface
- service implementation
- DI registration assumptions/tests
- all call sites

No compatibility shim or `[Obsolete]` bridge methods are kept.

### Examples of likely relocations

- Workflow-like calls currently in broad services (or currently absent and implemented in ViewModels) are migrated to dedicated workflow services.
- Utility/helper methods in ViewModel modules that perform persistence are replaced by service methods and deleted from helper modules.

## Affected ViewModel Areas

- Popups
  - `AddFixedExpenseVM`
  - `AddSavingGoalVM`
  - `AddSpendingSourceVM`
  - `ExpenseDetailVM`
  - `QuickAddVM`
  - `TransferFundsVM`
  - `SpendingSourceDetailVM`
  - `PlanningPopupVM`
  - `Settings*TabVM`, `SettingsVM`, `SettingsShared`
  - `GoalUpdateTransactionSupport`
- Quick Setup Wizard
  - `QuickSetupWizard*VM`
  - `QuickSetupWizardShared`

## Data Flow

1. ViewModels collect/validate UI input.
2. ViewModels call dedicated service methods with typed request models.
3. Services execute repository operations inside service-owned transaction boundaries.
4. Services return typed outcomes (`success/failure`, payload, and error message when needed).
5. ViewModels publish existing UI messages (`DashboardDataInvalidatedMessage`, `RecordLogMemoryMessage`, etc.) using returned data.

## Error Handling

- Service methods return deterministic failure results for user-recoverable errors.
- Exceptions are reserved for unexpected infrastructure/runtime faults and are translated to current user-facing error format at VM boundary.
- Existing error message style remains stable unless current text is clearly incorrect.

## Testing Strategy

### Unit Tests

- Add tests for each new workflow service method (success, validation failure, guarded delete/update, transactional side effects).
- Update ViewModel tests to substitute new service interfaces.
- Remove tests for deleted legacy methods and add replacements for new service contracts.

### DI/Infrastructure Tests

- Update service registration tests for new interfaces/implementations.
- Preserve lifetime safety checks; extend them to assert no forbidden persistence dependencies in targeted ViewModels.

### Regression Verification

- Build solution and run full test suite.
- Assert zero `IUnitOfWork` references under `Fluxo/ViewModels/**`.
- Spot-check key flows:
  - Quick add expense/income/goal
  - Transfer funds
  - Settings batch actions and maintenance actions
  - Quick setup wizard apply/finalization

## Implementation Sequencing

1. Introduce new service interfaces/implementations and request/response models.
2. Wire DI for new services.
3. Migrate ViewModel helpers to use services (delete `IUnitOfWork`-based helper persistence methods).
4. Migrate Popup and Settings ViewModels.
5. Migrate Quick Setup Wizard ViewModels/shared module.
6. Remove relocated legacy methods from old services.
7. Update tests and run verification.

## Risks and Mitigations

- Risk: Behavior drift during transaction migration.
  - Mitigation: Keep existing message emissions and mirror current logic paths with focused service tests.
- Risk: Partial migration leaves hidden `IUnitOfWork` references.
  - Mitigation: explicit post-migration grep/check over `Fluxo/ViewModels/**`.
- Risk: Overgrown new services.
  - Mitigation: split by workflow domain (settings, quick transactions, spending source workflow, wizard workflow).

## Acceptance Criteria

- No `IUnitOfWork` usage in `Fluxo/ViewModels/**`.
- ViewModel helper modules no longer accept or use `IUnitOfWork`.
- New dedicated workflow services own former VM-side persistence workflows.
- Relocated legacy methods are removed from old services and call sites.
- All tests pass after updates.
