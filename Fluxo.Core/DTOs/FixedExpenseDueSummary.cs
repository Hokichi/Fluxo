using Fluxo.Core.Enums;

namespace Fluxo.Core.DTOs;

public sealed class FixedExpenseDueSummary
{
    public int FixedExpenseId { get; init; }
    public string Name { get; init; } = string.Empty;
    public ExpenseCategory Category { get; init; }

    /// <summary>Known amount (null for Variable expenses — user must enter it).</summary>
    public decimal? Amount { get; init; }

    /// <summary>Historical average for variable expenses, to give the user a reference.</summary>
    public decimal? AverageHistoricalAmount { get; init; }

    public int DueDay { get; init; }
    public DateTime DueDate { get; init; }

    /// <summary>Days until DueDate. Negative = already overdue.</summary>
    public int DaysUntilDue => (DueDate.Date - DateTime.Today).Days;

    public bool IsOverdue => DaysUntilDue < 0;
    public bool IsDueToday => DaysUntilDue == 0;

    /// <summary>Whether the user still needs to input the amount this cycle.</summary>
    public bool RequiresAmountInput { get; init; }

    /// <summary>Tags for display/filtering.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];
}