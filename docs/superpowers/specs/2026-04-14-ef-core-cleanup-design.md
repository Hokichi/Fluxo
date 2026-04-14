# EF Core Cleanup — Design Spec
**Date:** 2026-04-14

## Overview

Refactor the data access and service layers to introduce a DTO layer, clean up the repository interfaces, add a full service layer, and remove the ViewModel-adapter pattern from the UI project.

The dependency direction after this change:
```
Fluxo (UI) → Fluxo.Services → Fluxo.Data → Fluxo.Core
```

Services return DTOs. The UI maps DTOs to ViewModels via AutoMapper.

---

## 1. DTO Layer

**Location:** `Fluxo.Core/DTO/` (namespace `Fluxo.Core.DTO`)

One DTO per entity. Plain classes — no ObservableObject, no attributes. Navigation properties are nested DTOs, never entity types.

| File | Properties |
|------|-----------|
| `ExpenseDto.cs` | Id, SpendingSourceId, ExpenseTagId, SpendingSource (SpendingSourceDto), ExpenseTag (ExpenseTagDto), Name, Amount, ExpenseKind, ExpenseCategory, RecurringDate, IsActive |
| `ExpenseLogDto.cs` | Id, ExpenseId, SpendingSourceId, Expense (ExpenseDto), SpendingSource (SpendingSourceDto), Amount, DeductedOn, Notes, IsForDeletion |
| `ExpenseTagDto.cs` | Id, Name, HexCode |
| `IncomeLogDto.cs` | Id, SpendingSourceId, SpendingSource (SpendingSourceDto), Amount, AddedOn, Notes |
| `SavingGoalDto.cs` | Id, Name, TargetAmount, CurrentAmount, SavingEndDate |
| `SpendingSourceDto.cs` | Id, Name, SpendingSourceType, AccountLimit, SpentAmount, Balance, DueDate, InterestRate, ShowOnUI, IsEnabled, IsForDeletion, **MoneyIn**, **MoneyOut** |
| `UserSettingsDto.cs` | Name, Value |

`SpendingSourceDto.MoneyIn` and `MoneyOut` are not entity properties — they are populated by the service from IncomeLog/ExpenseLog aggregates, not by AutoMapper.

---

## 2. AutoMapper Profiles

### Removed
- `Fluxo/Mappings/EntityViewModelProfile.cs` — direct Entity↔ViewModel mapping is removed entirely.

### Added: `EntityDtoProfile`
**Location:** `Fluxo.Services/Mappings/EntityDtoProfile.cs`

- Maps Entity → DTO (one direction, no ReverseMap)
- `SpendingSourceDto.MoneyIn` and `MoneyOut` are ignored (`ForMember(..., opt => opt.Ignore())`) — populated by service logic, not mapping
- All other properties map by convention (name match)

### Added: `DtoViewModelProfile`
**Location:** `Fluxo/Mappings/DtoViewModelProfile.cs`

- Maps DTO ↔ ViewModel (ReverseMap, same as the old EntityViewModelProfile)
- `SpendingSourceVM` computed properties (`PrimaryAmount`, `IsCashOrChecking`, etc.) are read-only and have no setter — AutoMapper ignores them automatically

---

## 3. Repository Layer

All interfaces extend `IRepository<T>` (unchanged), which provides `GetByIdAsync`, `AddAsync`, `Update`, `Remove`, `SaveChangesAsync`.

### `IExpenseRepository` / `ExpenseRepository`

| Method | Notes |
|--------|-------|
| `GetAllAsync()` | Includes ExpenseTag + SpendingSource navigations |
| `GetByExpenseIdAsync(int id)` | Renamed from GetByIdAsync for clarity; includes navigations |
| `SearchAsync(ExpenseFilter filter)` | Replaces all GetByDay/GetByMonth/GetByKind/GetByCategory/GetByTagId methods. Applies only non-null filter fields. `ShouldFilterDeletion` is unused here (applies to ExpenseLog) |
| `AddAsync(Expense entity)` | Inherited from base |
| `Update(Expense entity)` | Inherited from base |
| `Remove(Expense entity)` | Inherited from base |

Old methods removed: `GetByDayAsync`, `GetByWeekAsync`, `GetByMonthAsync`, `GetByKindAsync`, `GetByCategoryAsync`, `GetByTagIdAsync`.

### `IExpenseLogRepository` / `ExpenseLogRepository`

| Method | Notes |
|--------|-------|
| `GetAllAsync()` | Includes Expense + SpendingSource navigations |
| `GetByLogIdAsync(int id)` | Renamed from GetByIdAsync |
| `GetByExpenseIdAsync(int expenseId)` | Already exists |
| `GetMarkedForDeletionAsync()` | New — `WHERE IsForDeletion = true`; used by PostTerminationCleanup |
| `AddAsync(ExpenseLog entity)` | Inherited from base |
| `Update(ExpenseLog entity)` | Inherited from base |
| `Remove(ExpenseLog entity)` | Inherited from base |

Old methods removed: `GetByDayAsync`, `GetByWeekAsync`, `GetByMonthAsync`, `GetByCategoryAsync`, `GetBySpendingSourceIdAsync`.

### `ISpendingSourceRepository` / `SpendingSourceRepository`

| Method | Notes |
|--------|-------|
| `GetAllAsync()` | Inherited from base |
| `GetByIdAsync(int id)` | Inherited from base; used by AddIncome |
| `SearchAsync(SpendingSourceFilter filter)` | Replaces GetByDateAsync/GetBySourceTypeAsync. Filters by Name (contains), Type, ShowOnUIOnly, EnabledOnly |
| `GetMarkedForDeletionAsync()` | New — `WHERE IsForDeletion = true`; used by SpendingSourceService.DeleteAsync |
| `AddAsync(SpendingSource entity)` | Inherited from base |
| `Update(SpendingSource entity)` | Inherited from base |
| `Remove(SpendingSource entity)` | Inherited from base |

Old methods removed: `GetByDateAsync`, `GetBySourceTypeAsync`.

### `IIncomeLogRepository` / `IncomeLogRepository`

| Method | Notes |
|--------|-------|
| `GetAllAsync()` | Includes SpendingSource navigation |
| `SearchAsync(IncomeLogFilter filter)` | Replaces GetByDay/GetByWeek/GetByMonth/GetBySpendingSourceId. Filters by SpendingSource and date range |
| `AddAsync(IncomeLog entity)` | Inherited from base |
| `Update(IncomeLog entity)` | Inherited from base |
| `Remove(IncomeLog entity)` | Inherited from base |

Old methods removed: `GetByDayAsync`, `GetByWeekAsync`, `GetByMonthAsync`, `GetBySpendingSourceIdAsync`.

### `IUnitOfWork` — unchanged

All four repositories remain exposed. `SaveChangesAsync` is the commit point.

---

## 4. Service Layer

**Interfaces:** `Fluxo.Core/Interfaces/Services/`
**Implementations:** `Fluxo.Services/`
**Dependencies per service:** `IUnitOfWork`, `IMapper`

### `IExpenseService` / `ExpenseService`

| Method | Behaviour |
|--------|-----------|
| `GetAllAsync()` | `Expenses.GetAllAsync()` → map to `IReadOnlyList<ExpenseDto>` |
| `SearchAsync(ExpenseFilter filter)` | `Expenses.SearchAsync(filter)` → map to `IReadOnlyList<ExpenseDto>` |
| `AddAsync(ExpenseDto dto)` | Map DTO → Expense entity → `Expenses.AddAsync`. Create `ExpenseLog` (Amount, DeductedOn = now, Notes = empty string, SpendingSourceId from dto). Fetch SpendingSource → `Balance -= dto.Amount`, `SpentAmount += dto.Amount` → `SpendingSources.Update`. `SaveChangesAsync`. |
| `UpdateAsync(ExpenseDto dto)` | Map DTO → entity → `Expenses.Update` → `SaveChangesAsync` |
| `RemoveAsync(int id)` | Fetch expense logs via `ExpenseLogs.GetByExpenseIdAsync(id)` → `Remove` each. Fetch expense → `Expenses.Remove` → `SaveChangesAsync` |

### `IExpenseLogService` / `ExpenseLogService`

| Method | Behaviour |
|--------|-----------|
| `GetAllAsync()` | `ExpenseLogs.GetAllAsync()` → map to `IReadOnlyList<ExpenseLogDto>` |
| `DeleteAsync(int id)` | `ExpenseLogs.GetByLogIdAsync(id)` → set `IsForDeletion = true` → `Update` → `SaveChangesAsync` |
| `PostTerminationCleanupAsync()` | `ExpenseLogs.GetMarkedForDeletionAsync()` → `Remove` each log. Find orphaned expenses (expenses whose id no longer appears in any remaining log) → `Remove` each → `SaveChangesAsync` |

`ExpenseCleanupService` is made redundant by this service and should be deleted.

### `ISpendingSourceService` / `SpendingSourceService`

| Method | Behaviour |
|--------|-----------|
| `GetAllAsync()` | `SpendingSources.GetAllAsync()` → map to `IReadOnlyList<SpendingSourceDto>` |
| `SearchAsync(SpendingSourceFilter filter)` | `SpendingSources.SearchAsync(filter)` → map to `IReadOnlyList<SpendingSourceDto>` |
| `AddAsync(SpendingSourceDto dto)` | Map DTO → entity → `SpendingSources.AddAsync` → `SaveChangesAsync` |
| `DeleteAsync()` | `SpendingSources.GetMarkedForDeletionAsync()` → `Remove` each → `SaveChangesAsync` |
| `AddIncomeAsync(int spendingSourceId, decimal amount, string notes)` | `SpendingSources.GetByIdAsync(id)` → `Balance += amount` → `Update`. Create `IncomeLog` (Amount, AddedOn = now, Notes, SpendingSourceId) → `IncomeLogs.AddAsync` → `SaveChangesAsync` |

### `ITagService` / `TagService`

| Method | Behaviour |
|--------|-----------|
| `GetAllAsync()` | `ExpenseTags.GetAllAsync()` → map to `IReadOnlyList<ExpenseTagDto>` |
| `GetTagsOrderedByExpenseCountAsync(ExpenseFilter filter)` | `Expenses.SearchAsync(filter)` → group by `ExpenseTagId` → order by count descending → map each tag → `IReadOnlyList<ExpenseTagDto>` |
| `AddAsync(ExpenseTagDto dto)` | Map DTO → entity → `ExpenseTags.AddAsync` → `SaveChangesAsync` |
| `UpdateAsync(ExpenseTagDto dto)` | Map DTO → entity → `ExpenseTags.Update` → `SaveChangesAsync` |
| `RemoveAsync(int id)` | `ExpenseTags.GetByIdAsync(id)` → `ExpenseTags.Remove` → `SaveChangesAsync` |

---

## 5. UI Layer Cleanup

### Removed files

| File | Reason |
|------|--------|
| `Fluxo/Mappings/EntityViewModelProfile.cs` | Replaced by DtoViewModelProfile |
| `Fluxo/ViewModels/Persistence/ViewModelReadRepository.cs` | Adapter pattern removed |
| `Fluxo/ViewModels/Persistence/ViewModelWriteRepository.cs` | Adapter pattern removed |
| `Fluxo/ViewModels/Persistence/ExpenseViewModelReadRepository.cs` | Adapter pattern removed |
| `Fluxo/ViewModels/Persistence/ExpenseLogViewModelReadRepository.cs` | Adapter pattern removed |
| `Fluxo/ViewModels/Persistence/IncomeLogViewModelReadRepository.cs` | Adapter pattern removed |
| `Fluxo/ViewModels/Persistence/ExpenseTagViewModelReadRepository.cs` | Adapter pattern removed |
| `Fluxo/ViewModels/Persistence/SpendingSourceViewModelReadRepository.cs` | Adapter pattern removed |
| `Fluxo/ViewModels/Persistence/EntityViewModelReadUnitOfWork.cs` | Adapter pattern removed |
| `Fluxo/ViewModels/Persistence/EntityViewModelWriteUnitOfWork.cs` | Adapter pattern removed |
| `Fluxo.Core/Interfaces/IViewModelReadUnitOfWork.cs` | Adapter pattern removed |
| `Fluxo.Core/Interfaces/IViewModelWriteUnitOfWork.cs` | Adapter pattern removed |
| `Fluxo.Services/Persistence/ExpenseCleanupService.cs` | Superseded by ExpenseLogService |

### Updated

ViewModels that currently inject `IViewModelReadUnitOfWork` / `IViewModelWriteUnitOfWork` are updated to inject the appropriate service interface (e.g. `IExpenseService`, `ISpendingSourceService`) and `IMapper`. Service calls return DTOs; ViewModels map to their observable type via `_mapper.Map<ExpenseVM>(dto)`.

DI registrations in `Fluxo/Extensions/ServiceCollectionExtensions.cs` are updated to register all four service interfaces/implementations and the two new AutoMapper profiles.

---

## Out of Scope

- The IsForDeletion *marking* flow for SpendingSource (the UI action that sets `IsForDeletion = true` on a source) — deferred to a later phase
- FluentValidation validators — installed but not implemented; not part of this cleanup
- `SavingGoalRepository`, `UserSettingsRepository` — no changes required
- EF Core migrations — no schema changes except `SpendingSource.IsForDeletion` which is already merged from master
