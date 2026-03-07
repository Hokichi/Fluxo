using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class BudgetConfigRepository : BaseRepository<BudgetConfig>, IBudgetConfigRepository
{
    public BudgetConfigRepository(AppDbContext db) : base(db) { }

    public async Task<BudgetConfig?> GetByMonthAsync(int month, int year)
        => await Db.BudgetConfigs
            .FirstOrDefaultAsync(bc => bc.Month == month && bc.Year == year);

    public async Task UpsertAsync(BudgetConfig config)
    {
        var existing = await Db.BudgetConfigs
            .FirstOrDefaultAsync(bc => bc.Month == config.Month && bc.Year == config.Year);

        if (existing is null)
        {
            await Db.BudgetConfigs.AddAsync(config);
        }
        else
        {
            existing.NeedsPercentage = config.NeedsPercentage;
            existing.WantsPercentage = config.WantsPercentage;
            existing.SavingsPercentage = config.SavingsPercentage;
            existing.UpdatedAt = DateTime.UtcNow;
            Db.BudgetConfigs.Update(existing);
        }
    }
}