using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Services;

public interface IExpenseService
{
    Task<IReadOnlyList<Expense>> GetExpensesForMonthAsync(int month, int year);

    Task<IReadOnlyList<Expense>> GetByTagAsync(int tagId, int? month = null, int? year = null);

    Task<IReadOnlyList<Expense>> GetBnplExpensesAsync(int? bnplSourceId = null, int? month = null, int? year = null);

    /// <summary>
    /// Adds an expense. When date is null, uses DefaultEntryDay.
    /// When IsBnpl = true:
    ///   - BnplSourceId must be provided.
    ///   - BnplSource.CurrentBalance is incremented by Amount.
    ///   - BnplSetAsideAmount defaults to Amount if not supplied (lump-sum repayment model).
    /// Tags are applied by ID list.
    /// </summary>
    Task<Expense> AddExpenseAsync(
        string description,
        decimal amount,
        ExpenseCategory category,
        DateTime? date = null,
        bool isBnpl = false,
        int? bnplSourceId = null,
        decimal? bnplSetAsideAmount = null,
        int? bnplInstallmentCount = null,
        IEnumerable<int>? tagIds = null,
        string? notes = null);

    Task<Expense> UpdateExpenseAsync(Expense expense, IEnumerable<int>? newTagIds = null);

    /// <summary>
    /// Deletes an expense and reverses any BNPL balance adjustment that was
    /// made when the expense was originally recorded.
    /// </summary>
    Task DeleteExpenseAsync(int expenseId);
}