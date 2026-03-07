using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class FixedExpenseRepository : BaseRepository<FixedExpense>, IFixedExpenseRepository
{
    public FixedExpenseRepository(AppDbContext db) : base(db)
    {
    }

    public override async Task<FixedExpense?> GetByIdAsync(int id)
        => await Db.FixedExpenses
            .Include(fe => fe.FixedExpenseTags).ThenInclude(ft => ft.Tag)
            .Include(fe => fe.History)
            .FirstOrDefaultAsync(fe => fe.Id == id);

    public async Task<IReadOnlyList<FixedExpense>> GetAllActiveAsync()
        => await Db.FixedExpenses
            .Include(fe => fe.FixedExpenseTags).ThenInclude(ft => ft.Tag)
            .Where(fe => fe.IsActive)
            .OrderBy(fe => fe.DueDay)
            .ToListAsync();

    public async Task<IReadOnlyList<FixedExpense>> GetUnpaidForMonthAsync(int month, int year)
    {
        // Not paid at all, OR paid in a previous month (cycle reset).
        return await Db.FixedExpenses
            .Include(fe => fe.FixedExpenseTags).ThenInclude(ft => ft.Tag)
            .Where(fe => fe.IsActive &&
                         (fe.LastPaidDate == null ||
                          fe.LastPaidDate.Value.Month != month ||
                          fe.LastPaidDate.Value.Year != year))
            .OrderBy(fe => fe.DueDay)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<FixedExpense>> GetDueSoonAsync(int daysAhead)
    {
        var today = DateTime.Today;
        var cutoff = today.AddDays(daysAhead);
        return Db.FixedExpenses
            .Where(fe => fe.IsActive)
            .AsEnumerable() // evaluate DueDay → concrete date in memory
            .Where(fe =>
            {
                var dueDate = new DateTime(today.Year, today.Month,
                    Math.Min(fe.DueDay, DateTime.DaysInMonth(today.Year, today.Month)));
                // if DueDay already passed this month, look at next month
                if (dueDate < today)
                    dueDate = dueDate.AddMonths(1);
                return dueDate <= cutoff &&
                       (fe.LastPaidDate == null ||
                        fe.LastPaidDate.Value.Month != dueDate.Month ||
                        fe.LastPaidDate.Value.Year != dueDate.Year);
            })
            .OrderBy(fe => fe.DueDay)
            .ToList();
    }

    public async Task MarkAsPaidAsync(int id, DateTime paidDate)
    {
        var fe = await Db.FixedExpenses.FindAsync(id);
        if (fe is null) return;
        fe.LastPaidDate = paidDate;
        Db.FixedExpenses.Update(fe);
    }

    public async Task DeactivateAsync(int id)
    {
        var fe = await Db.FixedExpenses.FindAsync(id);
        if (fe is null) return;
        fe.IsActive = false;
        Db.FixedExpenses.Update(fe);
    }
}