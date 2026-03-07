using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IExpenseRepository : IRepository<Expense>
{
    Task<IReadOnlyList<Expense>> GetByMonthAsync(int month, int year);

    Task<IReadOnlyList<Expense>> GetByDateRangeAsync(DateTime from, DateTime to);

    Task<IReadOnlyList<Expense>> GetByCategoryAsync(ExpenseCategory category, int month, int year);

    Task<IReadOnlyList<Expense>> GetByTagAsync(int tagId, int? month = null, int? year = null);

    /// <summary>All BNPL expenses, optionally filtered to a specific source.</summary>
    Task<IReadOnlyList<Expense>> GetBnplExpensesAsync(int? bnplSourceId = null, int? month = null, int? year = null);

    /// <summary>
    /// Total set-aside amount from BNPL expenses for a given month.
    /// This is the grey "owed" overlay shown on the income dashboard.
    /// </summary>
    Task<decimal> GetBnplSetAsideTotalForMonthAsync(int month, int year);

    /// <summary>Sum of amounts per ExpenseCategory for the given month.</summary>
    Task<Dictionary<ExpenseCategory, decimal>> GetTotalsByCategoryAsync(int month, int year);
}