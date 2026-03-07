using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class BnplSourceRepository : BaseRepository<BnplSource>, IBnplSourceRepository
{
    public BnplSourceRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<BnplSource>> GetAllActiveAsync()
        => await Db.BnplSources
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .ToListAsync();

    public async Task AdjustBalanceAsync(int id, decimal delta)
    {
        var source = await Db.BnplSources.FindAsync(id)
                     ?? throw new InvalidOperationException($"BnplSource {id} not found.");
        source.CurrentBalance += delta;
        Db.BnplSources.Update(source);
    }

    public async Task DeactivateAsync(int id)
    {
        var source = await Db.BnplSources.FindAsync(id);
        if (source is null) return;
        source.IsActive = false;
        Db.BnplSources.Update(source);
    }
}