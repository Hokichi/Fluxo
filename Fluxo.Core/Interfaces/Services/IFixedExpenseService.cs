using Fluxo.Core.DTOs;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Services;

public interface IFixedExpenseService
{
    Task<IReadOnlyList<FixedExpense>> GetAllActiveAsync();

    /// <summary>
    /// Fixed expenses that are due within <paramref name="daysAhead"/> days and not yet paid.
    /// Used by the notification check and the dashboard "upcoming" panel.
    /// </summary>
    Task<IReadOnlyList<FixedExpenseDueSummary>> GetDueSoonAsync(int daysAhead);

    /// <summary>Unpaid fixed expenses for the current month, with historical average if variable.</summary>
    Task<IReadOnlyList<FixedExpenseDueSummary>> GetPendingForMonthAsync(int month, int year);

    /// <summary>
    /// Adds a fixed expense.
    /// Fixed mode: Amount must be provided.
    /// Variable mode: Amount should be null; user is prompted each cycle.
    /// </summary>
    Task<FixedExpense> AddFixedExpenseAsync(
        string name,
        FixedExpenseAmountMode amountMode,
        decimal? amount,
        int dueDay,
        ExpenseCategory category,
        bool notificationEnabled = true,
        IEnumerable<int>? tagIds = null,
        string? notes = null);

    Task<FixedExpense> UpdateFixedExpenseAsync(FixedExpense expense, IEnumerable<int>? newTagIds = null);

    /// <summary>
    /// Confirms that a bill has been paid for this cycle.
    /// Writes a FixedExpenseHistory row and updates LastPaidDate.
    /// For Variable expenses, <paramref name="amount"/> must be provided.
    /// For Fixed expenses, uses the configured amount if <paramref name="amount"/> is null.
    /// When <paramref name="paidDate"/> is null, uses today.
    /// </summary>
    Task ConfirmPaymentAsync(int fixedExpenseId, decimal? amount = null, DateTime? paidDate = null);

    Task DeactivateAsync(int fixedExpenseId);

    /// <summary>
    /// Sum of all fixed expense amounts due this month (using historical averages for variable ones).
    /// Passed to the budget and dashboard services.
    /// </summary>
    Task<decimal> GetEstimatedMonthlyTotalAsync(int month, int year);
}