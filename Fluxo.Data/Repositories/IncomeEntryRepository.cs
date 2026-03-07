using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class IncomeEntryRepository : BaseRepository<IncomeEntry>, IIncomeEntryRepository
{
    public IncomeEntryRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<IncomeEntry>> GetByMonthAsync(int month, int year)
        => await Db.IncomeEntries
            .Include(e => e.IncomeSource)
            .Where(e => e.Date.Month == month && e.Date.Year == year)
            .OrderByDescending(e => e.Date)
            .ToListAsync();

    public async Task<IReadOnlyList<IncomeEntry>> GetBySourceAsync(int sourceId)
        => await Db.IncomeEntries
            .Where(e => e.IncomeSourceId == sourceId)
            .OrderByDescending(e => e.Date)
            .ToListAsync();

    public async Task<IReadOnlyList<IncomeEntry>> GetByDateRangeAsync(DateTime from, DateTime to)
        => await Db.IncomeEntries
            .Include(e => e.IncomeSource)
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderByDescending(e => e.Date)
            .ToListAsync();

    public async Task<decimal> GetTotalForMonthAsync(int month, int year)
        => await Db.IncomeEntries
            .Where(e => e.Date.Month == month && e.Date.Year == year)
            .SumAsync(e => e.Amount);
}