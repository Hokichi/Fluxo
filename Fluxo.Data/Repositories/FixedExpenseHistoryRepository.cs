using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class FixedExpenseHistoryRepository : BaseRepository<FixedExpenseHistory>, IFixedExpenseHistoryRepository
{
    public FixedExpenseHistoryRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<FixedExpenseHistory>> GetByFixedExpenseAsync(int fixedExpenseId)
        => await Db.FixedExpenseHistory
            .Where(h => h.FixedExpenseId == fixedExpenseId)
            .OrderByDescending(h => h.PaidDate)
            .ToListAsync();

    public async Task<IReadOnlyList<FixedExpenseHistory>> GetByMonthAsync(int month, int year)
        => await Db.FixedExpenseHistory
            .Include(h => h.FixedExpense)
            .Where(h => h.PaidDate.Month == month && h.PaidDate.Year == year)
            .OrderByDescending(h => h.PaidDate)
            .ToListAsync();

    public async Task<IReadOnlyList<FixedExpenseHistory>> GetByDateRangeAsync(DateTime from, DateTime to)
        => await Db.FixedExpenseHistory
            .Include(h => h.FixedExpense)
            .Where(h => h.PaidDate >= from && h.PaidDate <= to)
            .OrderByDescending(h => h.PaidDate)
            .ToListAsync();

    public async Task<decimal?> GetAverageAmountAsync(int fixedExpenseId)
    {
        var rows = await Db.FixedExpenseHistory
            .Where(h => h.FixedExpenseId == fixedExpenseId)
            .Select(h => h.Amount)
            .ToListAsync();
        return rows.Count == 0 ? null : rows.Average();
    }

    public async Task<decimal> GetTotalForMonthAsync(int month, int year)
        => await Db.FixedExpenseHistory
            .Where(h => h.PaidDate.Month == month && h.PaidDate.Year == year)
            .SumAsync(h => h.Amount);
}