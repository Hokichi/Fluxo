using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class IncomeSourceRepository : BaseRepository<IncomeSource>, IIncomeSourceRepository
{
    public IncomeSourceRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<IncomeSource>> GetAllActiveAsync()
        => await Db.IncomeSources
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

    public async Task DeactivateAsync(int id)
    {
        var source = await Db.IncomeSources.FindAsync(id);
        if (source is null) return;
        source.IsActive = false;
        Db.IncomeSources.Update(source);
    }
}