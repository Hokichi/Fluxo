using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class ExpenseRepository : BaseRepository<Expense>, IExpenseRepository
{
    public ExpenseRepository(AppDbContext db) : base(db) { }

    public override async Task<Expense?> GetByIdAsync(int id)
        => await Db.Expenses
            .Include(e => e.BnplSource)
            .Include(e => e.ExpenseTags).ThenInclude(et => et.Tag)
            .FirstOrDefaultAsync(e => e.Id == id);

    public async Task<IReadOnlyList<Expense>> GetByMonthAsync(int month, int year)
        => await Db.Expenses
            .Include(e => e.BnplSource)
            .Include(e => e.ExpenseTags).ThenInclude(et => et.Tag)
            .Where(e => e.Date.Month == month && e.Date.Year == year)
            .OrderByDescending(e => e.Date)
            .ToListAsync();

    public async Task<IReadOnlyList<Expense>> GetByDateRangeAsync(DateTime from, DateTime to)
        => await Db.Expenses
            .Include(e => e.BnplSource)
            .Include(e => e.ExpenseTags).ThenInclude(et => et.Tag)
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderByDescending(e => e.Date)
            .ToListAsync();

    public async Task<IReadOnlyList<Expense>> GetByCategoryAsync(ExpenseCategory category, int month, int year)
        => await Db.Expenses
            .Where(e => e.Category == category && e.Date.Month == month && e.Date.Year == year)
            .ToListAsync();

    public async Task<IReadOnlyList<Expense>> GetByTagAsync(int tagId, int? month = null, int? year = null)
    {
        var query = Db.Expenses
            .Include(e => e.ExpenseTags).ThenInclude(et => et.Tag)
            .Where(e => e.ExpenseTags.Any(et => et.TagId == tagId));

        if (month.HasValue && year.HasValue)
            query = query.Where(e => e.Date.Month == month.Value && e.Date.Year == year.Value);

        return await query.OrderByDescending(e => e.Date).ToListAsync();
    }

    public async Task<IReadOnlyList<Expense>> GetBnplExpensesAsync(int? bnplSourceId = null, int? month = null, int? year = null)
    {
        var query = Db.Expenses
            .Include(e => e.BnplSource)
            .Where(e => e.IsBnpl);

        if (bnplSourceId.HasValue) query = query.Where(e => e.BnplSourceId == bnplSourceId);
        if (month.HasValue && year.HasValue)
            query = query.Where(e => e.Date.Month == month.Value && e.Date.Year == year.Value);

        return await query.OrderByDescending(e => e.Date).ToListAsync();
    }

    public async Task<decimal> GetBnplSetAsideTotalForMonthAsync(int month, int year)
        => await Db.Expenses
            .Where(e => e.IsBnpl && e.Date.Month == month && e.Date.Year == year)
            .SumAsync(e => e.BnplSetAsideAmount ?? 0m);

    public async Task<Dictionary<ExpenseCategory, decimal>> GetTotalsByCategoryAsync(int month, int year)
        => await Db.Expenses
            .Where(e => e.Date.Month == month && e.Date.Year == year)
            .GroupBy(e => e.Category)
            .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.Category, x => x.Total);
}