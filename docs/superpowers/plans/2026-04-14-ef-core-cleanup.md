# EF Core Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introduce a DTO layer, replace granular filter methods on repositories with composable `SearchAsync()`, implement a full service layer (4 services), and remove the ViewModel adapter pattern from the UI project.

**Architecture:** Services (`Fluxo.Services`) sit between UI and data. Repositories return entities; services map Entity → DTO via `EntityDtoProfile`. The UI maps DTO → ViewModel via `DtoViewModelProfile`. The ViewModel read/write repository adapters and ViewModel unit-of-work wrappers are deleted entirely.

**Tech Stack:** .NET 10, C#, EF Core 10 (SQLite), AutoMapper 16, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection.

---

## File Map

### Created
| File | Purpose |
|------|---------|
| `Fluxo.Core/DTO/ExpenseDto.cs` | DTO for Expense |
| `Fluxo.Core/DTO/ExpenseLogDto.cs` | DTO for ExpenseLog |
| `Fluxo.Core/DTO/ExpenseTagDto.cs` | DTO for ExpenseTag |
| `Fluxo.Core/DTO/IncomeLogDto.cs` | DTO for IncomeLog |
| `Fluxo.Core/DTO/SavingGoalDto.cs` | DTO for SavingGoal |
| `Fluxo.Core/DTO/SpendingSourceDto.cs` | DTO for SpendingSource (+ MoneyIn/MoneyOut) |
| `Fluxo.Core/DTO/UserSettingsDto.cs` | DTO for UserSettings |
| `Fluxo.Core/Interfaces/Services/IExpenseService.cs` | Expense service contract |
| `Fluxo.Core/Interfaces/Services/IExpenseLogService.cs` | ExpenseLog service contract |
| `Fluxo.Core/Interfaces/Services/ISpendingSourceService.cs` | SpendingSource service contract |
| `Fluxo.Core/Interfaces/Services/ITagService.cs` | Tag service contract |
| `Fluxo.Services/Mappings/EntityDtoProfile.cs` | AutoMapper: Entity ↔ DTO |
| `Fluxo.Services/ExpenseService.cs` | ExpenseService implementation |
| `Fluxo.Services/ExpenseLogService.cs` | ExpenseLogService implementation |
| `Fluxo.Services/SpendingSourceService.cs` | SpendingSourceService implementation |
| `Fluxo.Services/TagService.cs` | TagService implementation |
| `Fluxo/Mappings/DtoViewModelProfile.cs` | AutoMapper: DTO ↔ ViewModel |

### Modified
| File | Change |
|------|--------|
| `Fluxo.Core/Interfaces/Repositories/IExpenseRepository.cs` | Replace filter methods with `GetByExpenseIdAsync` + `SearchAsync` |
| `Fluxo.Core/Interfaces/Repositories/IExpenseLogRepository.cs` | Replace filter methods with `GetByLogIdAsync`, `GetByExpenseIdAsync`, `GetMarkedForDeletionAsync` |
| `Fluxo.Core/Interfaces/Repositories/ISpendingSourceRepository.cs` | Replace filter methods with `SearchAsync` + `GetMarkedForDeletionAsync` |
| `Fluxo.Core/Interfaces/Repositories/IIncomeLogRepository.cs` | Replace filter methods with `SearchAsync` |
| `Fluxo.Data/Repositories/ExpenseRepository.cs` | Implement `SearchAsync`, rename `GetByIdAsync` to `GetByExpenseIdAsync` |
| `Fluxo.Data/Repositories/ExpenseLogRepository.cs` | Add `GetByLogIdAsync`, `GetByExpenseIdAsync`, `GetMarkedForDeletionAsync`; remove old methods |
| `Fluxo.Data/Repositories/SpendingSourceRepository.cs` | Add `SearchAsync`, `GetMarkedForDeletionAsync`; remove old methods |
| `Fluxo.Data/Repositories/IncomeLogRepository.cs` | Add `SearchAsync`; remove old methods |
| `Fluxo/Extensions/ServiceCollectionExtensions.cs` | Replace adapter registrations with service + profile registrations |

### Deleted
| File | Reason |
|------|--------|
| `Fluxo/Mappings/EntityViewModelProfile.cs` | Replaced by `DtoViewModelProfile` |
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
| `Fluxo.Core/Interfaces/Repositories/IExpenseReadRepository.cs` | VM-specific read interface removed |
| `Fluxo.Core/Interfaces/Repositories/IExpenseLogReadRepository.cs` | VM-specific read interface removed |
| `Fluxo.Core/Interfaces/Repositories/IExpenseTagReadRepository.cs` | VM-specific read interface removed |
| `Fluxo.Core/Interfaces/Repositories/IIncomeLogReadRepository.cs` | VM-specific read interface removed |
| `Fluxo.Core/Interfaces/Repositories/ISpendingSourceReadRepository.cs` | VM-specific read interface removed |
| `Fluxo.Services/Persistence/ExpenseCleanupService.cs` | Superseded by `ExpenseLogService` |

---

## Task 1: Create DTO classes

**Files:**
- Create: `Fluxo.Core/DTO/ExpenseTagDto.cs`
- Create: `Fluxo.Core/DTO/SpendingSourceDto.cs`
- Create: `Fluxo.Core/DTO/ExpenseDto.cs`
- Create: `Fluxo.Core/DTO/ExpenseLogDto.cs`
- Create: `Fluxo.Core/DTO/IncomeLogDto.cs`
- Create: `Fluxo.Core/DTO/SavingGoalDto.cs`
- Create: `Fluxo.Core/DTO/UserSettingsDto.cs`

- [ ] **Step 1: Create leaf DTOs (no DTO navigation dependencies)**

`Fluxo.Core/DTO/ExpenseTagDto.cs`:
```csharp
namespace Fluxo.Core.DTO;

public class ExpenseTagDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string HexCode { get; set; } = string.Empty;
}
```

`Fluxo.Core/DTO/SpendingSourceDto.cs`:
```csharp
using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public class SpendingSourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SpendingSourceType SpendingSourceType { get; set; }
    public decimal AccountLimit { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal Balance { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? InterestRate { get; set; }
    public bool ShowOnUI { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsForDeletion { get; set; }
    /// <summary>Not mapped from entity — populated by service from IncomeLog aggregates.</summary>
    public decimal MoneyIn { get; set; }
    /// <summary>Not mapped from entity — populated by service from ExpenseLog aggregates.</summary>
    public decimal MoneyOut { get; set; }
}
```

`Fluxo.Core/DTO/SavingGoalDto.cs`:
```csharp
namespace Fluxo.Core.DTO;

public class SavingGoalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime SavingEndDate { get; set; }
}
```

`Fluxo.Core/DTO/UserSettingsDto.cs`:
```csharp
namespace Fluxo.Core.DTO;

public class UserSettingsDto
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Create composite DTOs (reference other DTOs)**

`Fluxo.Core/DTO/ExpenseDto.cs`:
```csharp
using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public class ExpenseDto
{
    public int Id { get; set; }
    public int SpendingSourceId { get; set; }
    public int ExpenseTagId { get; set; }
    public SpendingSourceDto SpendingSource { get; set; } = new();
    public ExpenseTagDto ExpenseTag { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public ExpenseKind ExpenseKind { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; }
    public DateTime? RecurringDate { get; set; }
    public bool IsActive { get; set; }
}
```

`Fluxo.Core/DTO/ExpenseLogDto.cs`:
```csharp
namespace Fluxo.Core.DTO;

public class ExpenseLogDto
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public int SpendingSourceId { get; set; }
    public ExpenseDto Expense { get; set; } = new();
    public SpendingSourceDto SpendingSource { get; set; } = new();
    public decimal Amount { get; set; }
    public DateTime DeductedOn { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsForDeletion { get; set; }
}
```

`Fluxo.Core/DTO/IncomeLogDto.cs`:
```csharp
namespace Fluxo.Core.DTO;

public class IncomeLogDto
{
    public int Id { get; set; }
    public int SpendingSourceId { get; set; }
    public SpendingSourceDto SpendingSource { get; set; } = new();
    public decimal Amount { get; set; }
    public DateTime AddedOn { get; set; }
    public string Notes { get; set; } = string.Empty;
}
```

- [ ] **Step 3: Build Fluxo.Core to verify**

```bash
dotnet build Fluxo.Core/Fluxo.Core.csproj
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Fluxo.Core/DTO/
git commit -m "feat: add DTO classes to Fluxo.Core.DTO"
```

---

## Task 2: Update repository interfaces

**Files:**
- Modify: `Fluxo.Core/Interfaces/Repositories/IExpenseRepository.cs`
- Modify: `Fluxo.Core/Interfaces/Repositories/IExpenseLogRepository.cs`
- Modify: `Fluxo.Core/Interfaces/Repositories/ISpendingSourceRepository.cs`
- Modify: `Fluxo.Core/Interfaces/Repositories/IIncomeLogRepository.cs`

- [ ] **Step 1: Replace IExpenseRepository**

`Fluxo.Core/Interfaces/Repositories/IExpenseRepository.cs`:
```csharp
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    Task<Expense?> GetByExpenseIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Expense>> SearchAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Replace IExpenseLogRepository**

`Fluxo.Core/Interfaces/Repositories/IExpenseLogRepository.cs`:
```csharp
using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseLogRepository : IRepository<ExpenseLog>
{
    Task<ExpenseLog?> GetByLogIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetByExpenseIdAsync(int expenseId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetMarkedForDeletionAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Replace ISpendingSourceRepository**

`Fluxo.Core/Interfaces/Repositories/ISpendingSourceRepository.cs`:
```csharp
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Repositories;

public interface ISpendingSourceRepository : IRepository<SpendingSource>
{
    Task<IReadOnlyList<SpendingSource>> SearchAsync(SpendingSourceFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpendingSource>> GetMarkedForDeletionAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Replace IIncomeLogRepository**

`Fluxo.Core/Interfaces/Repositories/IIncomeLogRepository.cs`:
```csharp
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IIncomeLogRepository : IRepository<IncomeLog>
{
    Task<IReadOnlyList<IncomeLog>> SearchAsync(IncomeLogFilter filter, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Build Fluxo.Core**

```bash
dotnet build Fluxo.Core/Fluxo.Core.csproj
```

Expected: `Build succeeded` with 0 errors. (Fluxo.Data will break — that's expected and fixed in Task 3.)

- [ ] **Step 6: Commit**

```bash
git add Fluxo.Core/Interfaces/Repositories/
git commit -m "refactor: replace granular filter methods with SearchAsync on repository interfaces"
```

---

## Task 3: Implement repository changes

**Files:**
- Modify: `Fluxo.Data/Repositories/ExpenseRepository.cs`
- Modify: `Fluxo.Data/Repositories/ExpenseLogRepository.cs`
- Modify: `Fluxo.Data/Repositories/SpendingSourceRepository.cs`
- Modify: `Fluxo.Data/Repositories/IncomeLogRepository.cs`

- [ ] **Step 1: Rewrite ExpenseRepository**

`Fluxo.Data/Repositories/ExpenseRepository.cs`:
```csharp
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseRepository(FluxoDbContext dbContext)
    : Repository<Expense>(dbContext), IExpenseRepository
{
    public override async Task<IReadOnlyList<Expense>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations().ToListAsync(cancellationToken);
    }

    public async Task<Expense?> GetByExpenseIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (FindTrackedEntity(id) is { } tracked)
            return tracked;

        return await QueryWithNavigations()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Expense>> SearchAsync(ExpenseFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = QueryWithNavigations();

        if (!string.IsNullOrWhiteSpace(filter.Name))
            query = query.Where(e => e.Name.Contains(filter.Name));

        if (filter.StartDate.HasValue)
            query = query.Where(e => e.RecurringDate >= filter.StartDate);

        if (filter.EndDate.HasValue)
            query = query.Where(e => e.RecurringDate <= filter.EndDate);

        if (filter.Category.HasValue)
            query = query.Where(e => e.ExpenseCategory == filter.Category);

        if (filter.Kind.HasValue)
            query = query.Where(e => e.ExpenseKind == filter.Kind);

        if (filter.TagId.HasValue)
            query = query.Where(e => e.ExpenseTagId == filter.TagId);
        else if (filter.Tag is not null)
            query = query.Where(e => e.ExpenseTagId == filter.Tag.Id);

        return await query.ToListAsync(cancellationToken);
    }

    private IQueryable<Expense> QueryWithNavigations()
    {
        return DbSet
            .AsNoTrackingWithIdentityResolution()
            .Include(e => e.ExpenseTag)
            .Include(e => e.SpendingSource);
    }
}
```

- [ ] **Step 2: Rewrite ExpenseLogRepository**

`Fluxo.Data/Repositories/ExpenseLogRepository.cs`:
```csharp
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseLogRepository(FluxoDbContext dbContext)
    : Repository<ExpenseLog>(dbContext), IExpenseLogRepository
{
    public override async Task<IReadOnlyList<ExpenseLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations().ToListAsync(cancellationToken);
    }

    public async Task<ExpenseLog?> GetByLogIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (FindTrackedEntity(id) is { } tracked)
            return tracked;

        return await QueryWithNavigations()
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetByExpenseIdAsync(int expenseId,
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => log.ExpenseId == expenseId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseLog>> GetMarkedForDeletionAsync(
        CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations()
            .Where(log => log.IsForDeletion)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ExpenseLog> QueryWithNavigations()
    {
        return DbSet
            .AsNoTrackingWithIdentityResolution()
            .Include(log => log.Expense)
            .ThenInclude(e => e.ExpenseTag)
            .Include(log => log.Expense)
            .ThenInclude(e => e.SpendingSource)
            .Include(log => log.SpendingSource);
    }
}
```

- [ ] **Step 3: Rewrite SpendingSourceRepository**

`Fluxo.Data/Repositories/SpendingSourceRepository.cs`:
```csharp
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class SpendingSourceRepository(FluxoDbContext dbContext)
    : Repository<SpendingSource>(dbContext), ISpendingSourceRepository
{
    public async Task<IReadOnlyList<SpendingSource>> SearchAsync(SpendingSourceFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.Name))
            query = query.Where(s => s.Name.Contains(filter.Name));

        if (filter.Type.HasValue)
            query = query.Where(s => s.SpendingSourceType == filter.Type);

        if (filter.ShowOnUIOnly)
            query = query.Where(s => s.ShowOnUI);

        if (filter.EnabledOnly)
            query = query.Where(s => s.IsEnabled);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SpendingSource>> GetMarkedForDeletionAsync(
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(s => s.IsForDeletion)
            .ToListAsync(cancellationToken);
    }
}
```

- [ ] **Step 4: Rewrite IncomeLogRepository**

`Fluxo.Data/Repositories/IncomeLogRepository.cs`:
```csharp
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class IncomeLogRepository(FluxoDbContext dbContext)
    : Repository<IncomeLog>(dbContext), IIncomeLogRepository
{
    public override async Task<IReadOnlyList<IncomeLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await QueryWithNavigations().ToListAsync(cancellationToken);
    }

    public override async Task<IncomeLog?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (FindTrackedEntity(id) is { } tracked)
            return tracked;

        return await QueryWithNavigations()
            .FirstOrDefaultAsync(log => log.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<IncomeLog>> SearchAsync(IncomeLogFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = QueryWithNavigations();

        if (filter.SpendingSource is not null)
            query = query.Where(log => log.SpendingSourceId == filter.SpendingSource.Id);

        if (filter.StartDate.HasValue)
            query = query.Where(log => log.AddedOn >= filter.StartDate);

        if (filter.EndDate.HasValue)
            query = query.Where(log => log.AddedOn <= filter.EndDate);

        return await query.ToListAsync(cancellationToken);
    }

    private IQueryable<IncomeLog> QueryWithNavigations()
    {
        return DbSet
            .AsNoTrackingWithIdentityResolution()
            .Include(log => log.SpendingSource);
    }
}
```

- [ ] **Step 5: Build Fluxo.Data**

```bash
dotnet build Fluxo.Data/Fluxo.Data.csproj
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Fluxo.Data/Repositories/
git commit -m "refactor: implement SearchAsync and named lookup methods on repositories"
```

---

## Task 4: Create service interfaces

**Files:**
- Create: `Fluxo.Core/Interfaces/Services/IExpenseService.cs`
- Create: `Fluxo.Core/Interfaces/Services/IExpenseLogService.cs`
- Create: `Fluxo.Core/Interfaces/Services/ISpendingSourceService.cs`
- Create: `Fluxo.Core/Interfaces/Services/ITagService.cs`

- [ ] **Step 1: Create IExpenseService**

`Fluxo.Core/Interfaces/Services/IExpenseService.cs`:
```csharp
using Fluxo.Core.DTO;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Services;

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseDto>> SearchAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(ExpenseDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExpenseDto dto, CancellationToken cancellationToken = default);
    Task RemoveAsync(int id, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create IExpenseLogService**

`Fluxo.Core/Interfaces/Services/IExpenseLogService.cs`:
```csharp
using Fluxo.Core.DTO;

namespace Fluxo.Core.Interfaces.Services;

public interface IExpenseLogService
{
    Task<IReadOnlyList<ExpenseLogDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task PostTerminationCleanupAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Create ISpendingSourceService**

`Fluxo.Core/Interfaces/Services/ISpendingSourceService.cs`:
```csharp
using Fluxo.Core.DTO;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Services;

public interface ISpendingSourceService
{
    Task<IReadOnlyList<SpendingSourceDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpendingSourceDto>> SearchAsync(SpendingSourceFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(SpendingSourceDto dto, CancellationToken cancellationToken = default);
    Task DeleteAsync(CancellationToken cancellationToken = default);
    Task AddIncomeAsync(int spendingSourceId, decimal amount, string notes, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Create ITagService**

`Fluxo.Core/Interfaces/Services/ITagService.cs`:
```csharp
using Fluxo.Core.DTO;
using Fluxo.Core.Filters;

namespace Fluxo.Core.Interfaces.Services;

public interface ITagService
{
    Task<IReadOnlyList<ExpenseTagDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseTagDto>> GetTagsOrderedByExpenseCountAsync(ExpenseFilter filter, CancellationToken cancellationToken = default);
    Task AddAsync(ExpenseTagDto dto, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExpenseTagDto dto, CancellationToken cancellationToken = default);
    Task RemoveAsync(int id, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 5: Build Fluxo.Core**

```bash
dotnet build Fluxo.Core/Fluxo.Core.csproj
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 6: Commit**

```bash
git add Fluxo.Core/Interfaces/Services/
git commit -m "feat: add service interfaces to Fluxo.Core.Interfaces.Services"
```

---

## Task 5: Create EntityDtoProfile

**Files:**
- Create: `Fluxo.Services/Mappings/EntityDtoProfile.cs`

- [ ] **Step 1: Create the profile**

`Fluxo.Services/Mappings/EntityDtoProfile.cs`:
```csharp
using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;

namespace Fluxo.Services.Mappings;

public sealed class EntityDtoProfile : Profile
{
    public EntityDtoProfile()
    {
        CreateMap<Expense, ExpenseDto>().ReverseMap();
        CreateMap<ExpenseLog, ExpenseLogDto>().ReverseMap();
        CreateMap<ExpenseTag, ExpenseTagDto>().ReverseMap();
        CreateMap<IncomeLog, IncomeLogDto>().ReverseMap();
        CreateMap<SavingGoal, SavingGoalDto>().ReverseMap();
        CreateMap<UserSettings, UserSettingsDto>().ReverseMap();

        // SpendingSource: ignore computed fields when mapping Entity→DTO,
        // and ignore Id when mapping DTO→Entity (EF assigns Id on insert).
        CreateMap<SpendingSource, SpendingSourceDto>()
            .ForMember(dest => dest.MoneyIn, opt => opt.Ignore())
            .ForMember(dest => dest.MoneyOut, opt => opt.Ignore())
            .ReverseMap()
            .ForMember(dest => dest.Id, opt => opt.Ignore());
    }
}
```

> `ReverseMap()` is included on all mappings because service methods like `UpdateAsync` and `AddAsync` use `mapper.Map(dto, entity)` to apply DTO data onto a fetched entity. The `SpendingSource` reverse mapping ignores `Id` so that mapping a DTO into a fetched entity does not clobber the entity's database-assigned Id.

- [ ] **Step 2: Build Fluxo.Services**

```bash
dotnet build Fluxo.Services/Fluxo.Services.csproj
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fluxo.Services/Mappings/EntityDtoProfile.cs
git commit -m "feat: add EntityDtoProfile for Entity<->DTO AutoMapper mappings"
```

---

## Task 6: Implement ExpenseService

**Files:**
- Create: `Fluxo.Services/ExpenseService.cs`

- [ ] **Step 1: Create ExpenseService**

`Fluxo.Services/ExpenseService.cs`:
```csharp
using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services;

public sealed class ExpenseService(IUnitOfWork unitOfWork, IMapper mapper) : IExpenseService
{
    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var expenses = await unitOfWork.Expenses.GetAllAsync(cancellationToken);
        return mapper.Map<IReadOnlyList<ExpenseDto>>(expenses);
    }

    public async Task<IReadOnlyList<ExpenseDto>> SearchAsync(ExpenseFilter filter,
        CancellationToken cancellationToken = default)
    {
        var expenses = await unitOfWork.Expenses.SearchAsync(filter, cancellationToken);
        return mapper.Map<IReadOnlyList<ExpenseDto>>(expenses);
    }

    public async Task AddAsync(ExpenseDto dto, CancellationToken cancellationToken = default)
    {
        // Build the entity manually so EF can track it and resolve the ExpenseLog FK.
        var expense = new Expense
        {
            SpendingSourceId = dto.SpendingSourceId,
            ExpenseTagId = dto.ExpenseTagId,
            Name = dto.Name,
            Amount = dto.Amount,
            ExpenseKind = dto.ExpenseKind,
            ExpenseCategory = dto.ExpenseCategory,
            RecurringDate = dto.RecurringDate,
            IsActive = dto.IsActive
        };
        await unitOfWork.Expenses.AddAsync(expense, cancellationToken);

        // Link the log via navigation — EF resolves the FK after insert.
        var log = new ExpenseLog
        {
            Expense = expense,
            SpendingSourceId = dto.SpendingSourceId,
            Amount = dto.Amount,
            DeductedOn = DateTime.Now,
            Notes = string.Empty,
            IsForDeletion = false
        };
        await unitOfWork.ExpenseLogs.AddAsync(log, cancellationToken);

        // Deduct from spending source.
        var source = await unitOfWork.SpendingSources.GetByIdAsync(dto.SpendingSourceId, cancellationToken);
        if (source is not null)
        {
            source.Balance -= dto.Amount;
            source.SpentAmount += dto.Amount;
            unitOfWork.SpendingSources.Update(source);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ExpenseDto dto, CancellationToken cancellationToken = default)
    {
        var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(dto.Id, cancellationToken);
        if (expense is null) return;

        mapper.Map(dto, expense);
        unitOfWork.Expenses.Update(expense);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        // FK deletes are Restrict — remove logs before removing the expense.
        var logs = await unitOfWork.ExpenseLogs.GetByExpenseIdAsync(id, cancellationToken);
        foreach (var log in logs)
            unitOfWork.ExpenseLogs.Remove(log);

        var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(id, cancellationToken);
        if (expense is not null)
            unitOfWork.Expenses.Remove(expense);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 2: Build Fluxo.Services**

```bash
dotnet build Fluxo.Services/Fluxo.Services.csproj
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fluxo.Services/ExpenseService.cs
git commit -m "feat: implement ExpenseService"
```

---

## Task 7: Implement ExpenseLogService

**Files:**
- Create: `Fluxo.Services/ExpenseLogService.cs`

- [ ] **Step 1: Create ExpenseLogService**

`Fluxo.Services/ExpenseLogService.cs`:
```csharp
using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services;

public sealed class ExpenseLogService(IUnitOfWork unitOfWork, IMapper mapper) : IExpenseLogService
{
    public async Task<IReadOnlyList<ExpenseLogDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var logs = await unitOfWork.ExpenseLogs.GetAllAsync(cancellationToken);
        return mapper.Map<IReadOnlyList<ExpenseLogDto>>(logs);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var log = await unitOfWork.ExpenseLogs.GetByLogIdAsync(id, cancellationToken);
        if (log is null) return;

        log.IsForDeletion = true;
        unitOfWork.ExpenseLogs.Update(log);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task PostTerminationCleanupAsync(CancellationToken cancellationToken = default)
    {
        var markedLogs = await unitOfWork.ExpenseLogs.GetMarkedForDeletionAsync(cancellationToken);
        var expenseIds = markedLogs.Select(l => l.ExpenseId).Distinct().ToList();

        foreach (var log in markedLogs)
            unitOfWork.ExpenseLogs.Remove(log);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // After deleting logs, remove any expenses that have no remaining logs.
        foreach (var expenseId in expenseIds)
        {
            var remaining = await unitOfWork.ExpenseLogs.GetByExpenseIdAsync(expenseId, cancellationToken);
            if (remaining.Count > 0) continue;

            var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(expenseId, cancellationToken);
            if (expense is not null)
                unitOfWork.Expenses.Remove(expense);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 2: Build Fluxo.Services**

```bash
dotnet build Fluxo.Services/Fluxo.Services.csproj
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fluxo.Services/ExpenseLogService.cs
git commit -m "feat: implement ExpenseLogService"
```

---

## Task 8: Implement SpendingSourceService

**Files:**
- Create: `Fluxo.Services/SpendingSourceService.cs`

- [ ] **Step 1: Create SpendingSourceService**

`Fluxo.Services/SpendingSourceService.cs`:
```csharp
using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services;

public sealed class SpendingSourceService(IUnitOfWork unitOfWork, IMapper mapper) : ISpendingSourceService
{
    public async Task<IReadOnlyList<SpendingSourceDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sources = await unitOfWork.SpendingSources.GetAllAsync(cancellationToken);
        return mapper.Map<IReadOnlyList<SpendingSourceDto>>(sources);
    }

    public async Task<IReadOnlyList<SpendingSourceDto>> SearchAsync(SpendingSourceFilter filter,
        CancellationToken cancellationToken = default)
    {
        var sources = await unitOfWork.SpendingSources.SearchAsync(filter, cancellationToken);
        return mapper.Map<IReadOnlyList<SpendingSourceDto>>(sources);
    }

    public async Task AddAsync(SpendingSourceDto dto, CancellationToken cancellationToken = default)
    {
        var source = mapper.Map<SpendingSource>(dto);
        source.Id = 0; // ensure EF treats this as a new insert
        await unitOfWork.SpendingSources.AddAsync(source, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var marked = await unitOfWork.SpendingSources.GetMarkedForDeletionAsync(cancellationToken);
        foreach (var source in marked)
            unitOfWork.SpendingSources.Remove(source);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AddIncomeAsync(int spendingSourceId, decimal amount, string notes,
        CancellationToken cancellationToken = default)
    {
        var source = await unitOfWork.SpendingSources.GetByIdAsync(spendingSourceId, cancellationToken);
        if (source is null) return;

        source.Balance += amount;
        unitOfWork.SpendingSources.Update(source);

        var incomeLog = new IncomeLog
        {
            SpendingSourceId = spendingSourceId,
            Amount = amount,
            AddedOn = DateTime.Now,
            Notes = notes
        };
        await unitOfWork.IncomeLogs.AddAsync(incomeLog, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 2: Build Fluxo.Services**

```bash
dotnet build Fluxo.Services/Fluxo.Services.csproj
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fluxo.Services/SpendingSourceService.cs
git commit -m "feat: implement SpendingSourceService"
```

---

## Task 9: Implement TagService

**Files:**
- Create: `Fluxo.Services/TagService.cs`

- [ ] **Step 1: Create TagService**

`Fluxo.Services/TagService.cs`:
```csharp
using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services;

public sealed class TagService(IUnitOfWork unitOfWork, IMapper mapper) : ITagService
{
    public async Task<IReadOnlyList<ExpenseTagDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tags = await unitOfWork.ExpenseTags.GetAllAsync(cancellationToken);
        return mapper.Map<IReadOnlyList<ExpenseTagDto>>(tags);
    }

    public async Task<IReadOnlyList<ExpenseTagDto>> GetTagsOrderedByExpenseCountAsync(ExpenseFilter filter,
        CancellationToken cancellationToken = default)
    {
        // SearchAsync includes ExpenseTag navigations — group in memory after fetch.
        var expenses = await unitOfWork.Expenses.SearchAsync(filter, cancellationToken);

        var tags = expenses
            .Where(e => e.ExpenseTag is not null)
            .GroupBy(e => e.ExpenseTagId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.First().ExpenseTag!)
            .ToList();

        return mapper.Map<IReadOnlyList<ExpenseTagDto>>(tags);
    }

    public async Task AddAsync(ExpenseTagDto dto, CancellationToken cancellationToken = default)
    {
        var tag = mapper.Map<ExpenseTag>(dto);
        tag.Id = 0;
        await unitOfWork.ExpenseTags.AddAsync(tag, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ExpenseTagDto dto, CancellationToken cancellationToken = default)
    {
        var tag = await unitOfWork.ExpenseTags.GetByIdAsync(dto.Id, cancellationToken);
        if (tag is null) return;

        mapper.Map(dto, tag);
        unitOfWork.ExpenseTags.Update(tag);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await unitOfWork.ExpenseTags.GetByIdAsync(id, cancellationToken);
        if (tag is null) return;

        unitOfWork.ExpenseTags.Remove(tag);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
```

- [ ] **Step 2: Build Fluxo.Services**

```bash
dotnet build Fluxo.Services/Fluxo.Services.csproj
```

Expected: `Build succeeded` with 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fluxo.Services/TagService.cs
git commit -m "feat: implement TagService"
```

---

## Task 10: Create DtoViewModelProfile

**Files:**
- Create: `Fluxo/Mappings/DtoViewModelProfile.cs`

- [ ] **Step 1: Create DtoViewModelProfile**

`Fluxo/Mappings/DtoViewModelProfile.cs`:
```csharp
using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.ViewModels.Entities;

namespace Fluxo.Mappings;

public sealed class DtoViewModelProfile : Profile
{
    public DtoViewModelProfile()
    {
        CreateMap<ExpenseDto, ExpenseVM>().ReverseMap();
        CreateMap<ExpenseLogDto, ExpenseLogVM>().ReverseMap();
        CreateMap<IncomeLogDto, IncomeLogVM>().ReverseMap();
        CreateMap<ExpenseTagDto, ExpenseTagVM>().ReverseMap();
        CreateMap<SavingGoalDto, SavingGoalVM>().ReverseMap();
        CreateMap<SpendingSourceDto, SpendingSourceVM>().ReverseMap();
        CreateMap<UserSettingsDto, UserSettingsVM>().ReverseMap();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Fluxo/Mappings/DtoViewModelProfile.cs
git commit -m "feat: add DtoViewModelProfile for DTO<->ViewModel AutoMapper mappings"
```

---

## Task 11: Remove adapter pattern files

**Files:** Delete from `Fluxo/ViewModels/Persistence/`, `Fluxo.Core/Interfaces/`, `Fluxo/Mappings/`, `Fluxo.Services/Persistence/`

- [ ] **Step 1: Delete ViewModel persistence adapters**

```bash
git rm Fluxo/ViewModels/Persistence/ViewModelReadRepository.cs
git rm Fluxo/ViewModels/Persistence/ViewModelWriteRepository.cs
git rm Fluxo/ViewModels/Persistence/ExpenseViewModelReadRepository.cs
git rm Fluxo/ViewModels/Persistence/ExpenseLogViewModelReadRepository.cs
git rm Fluxo/ViewModels/Persistence/IncomeLogViewModelReadRepository.cs
git rm Fluxo/ViewModels/Persistence/ExpenseTagViewModelReadRepository.cs
git rm Fluxo/ViewModels/Persistence/SpendingSourceViewModelReadRepository.cs
git rm Fluxo/ViewModels/Persistence/EntityViewModelReadUnitOfWork.cs
git rm Fluxo/ViewModels/Persistence/EntityViewModelWriteUnitOfWork.cs
```

- [ ] **Step 2: Delete ViewModel UoW interfaces from Core**

```bash
git rm Fluxo.Core/Interfaces/IViewModelReadUnitOfWork.cs
git rm Fluxo.Core/Interfaces/IViewModelWriteUnitOfWork.cs
```

- [ ] **Step 3: Delete VM-specific repository interfaces from Core**

List the files first to confirm names:
```bash
ls Fluxo.Core/Interfaces/Repositories/
```

Delete only the generic ViewModel read repository interfaces (files with names like `IExpense**Read**Repository.cs`, `IExpenseLog**Read**Repository.cs`, etc.). Do **not** delete `IReadRepository.cs` or `IWriteRepository.cs` if those are base interfaces for `IRepository<T>`.

```bash
git rm Fluxo.Core/Interfaces/Repositories/IExpenseReadRepository.cs
git rm Fluxo.Core/Interfaces/Repositories/IExpenseLogReadRepository.cs
git rm Fluxo.Core/Interfaces/Repositories/IExpenseTagReadRepository.cs
git rm Fluxo.Core/Interfaces/Repositories/IIncomeLogReadRepository.cs
git rm Fluxo.Core/Interfaces/Repositories/ISpendingSourceReadRepository.cs
```

- [ ] **Step 4: Delete old profile and cleanup service**

```bash
git rm Fluxo/Mappings/EntityViewModelProfile.cs
git rm Fluxo.Services/Persistence/ExpenseCleanupService.cs
```

- [ ] **Step 5: Commit deletions**

```bash
git commit -m "refactor: remove ViewModel adapter pattern (read/write repositories, UoW wrappers, old profile)"
```

---

## Task 12: Update DI registrations and fix compile errors

**Files:**
- Modify: `Fluxo/Extensions/ServiceCollectionExtensions.cs`

- [ ] **Step 1: Rewrite AddFluxoPresentation**

Replace the entire content of `Fluxo/Extensions/ServiceCollectionExtensions.cs`:
```csharp
using AutoMapper;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Mappings;
using Fluxo.Services;
using Fluxo.Services.Mappings;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Shell.Main;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fluxo.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFluxoPresentation(this IServiceCollection services)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<EntityDtoProfile>();
            cfg.AddProfile<DtoViewModelProfile>();
        }, NullLoggerFactory.Instance);

        services.AddSingleton<IMapper>(_ => mapperConfig.CreateMapper());

        services.AddTransient<IExpenseService, ExpenseService>();
        services.AddTransient<IExpenseLogService, ExpenseLogService>();
        services.AddTransient<ISpendingSourceService, SpendingSourceService>();
        services.AddTransient<ITagService, TagService>();

        return services;
    }

    public static IServiceCollection AddUIData(this IServiceCollection services)
    {
        services.AddSingleton<MainVM>();
        services.AddSingleton<DayOfWeekVM>();
        services.AddTransient<ExpenseVM>();
        services.AddTransient<ExpenseLogVM>();
        services.AddTransient<IncomeLogVM>();
        services.AddTransient<ExpenseTagVM>();
        services.AddTransient<SavingGoalVM>();
        services.AddTransient<SpendingSourceVM>();
        services.AddTransient<UserSettingsVM>();

        services.AddSingleton<MainWindow>();

        return services;
    }
}
```

- [ ] **Step 2: Attempt full solution build to surface remaining errors**

```bash
dotnet build 2>&1
```

Expected: Compile errors in ViewModel files that previously injected `IViewModelReadUnitOfWork` or `IViewModelWriteUnitOfWork`. Note every erroring file path.

- [ ] **Step 3: Fix each erroring ViewModel**

For each file reported as erroring, apply this pattern:

**Before** (typical ViewModel that loads expenses):
```csharp
public partial class SomeVM(
    IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM> readUow,
    IMapper mapper)
{
    private async Task LoadAsync()
    {
        var expenses = await readUow.Expenses.GetAllAsync();
        Expenses = new ObservableCollection<ExpenseVM>(expenses);
    }
}
```

**After** — inject the appropriate service, map DTOs to ViewModels:
```csharp
public partial class SomeVM(IExpenseService expenseService, IMapper mapper)
{
    private async Task LoadAsync()
    {
        var dtos = await expenseService.GetAllAsync();
        Expenses = new ObservableCollection<ExpenseVM>(mapper.Map<IEnumerable<ExpenseVM>>(dtos));
    }
}
```

For write operations:

**Before**:
```csharp
var vm = new ExpenseVM { ... };
await writeUow.Expenses.AddAsync(vm);
await writeUow.SaveChangesAsync();
```

**After**:
```csharp
var dto = mapper.Map<ExpenseDto>(vm);
await expenseService.AddAsync(dto);
```

Service → ViewModel mapping reference:
| Data type needed | Inject |
|-----------------|--------|
| Expense data | `IExpenseService` |
| ExpenseLog data | `IExpenseLogService` |
| SpendingSource data | `ISpendingSourceService` |
| Tag data | `ITagService` |

- [ ] **Step 4: Build until clean**

```bash
dotnet build 2>&1
```

Expected: `Build succeeded` with 0 errors and 0 warnings (or only pre-existing warnings unrelated to this change).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "refactor: wire services into UI layer, remove ViewModel UoW injection"
```
