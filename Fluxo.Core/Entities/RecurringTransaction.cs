using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

public sealed class RecurringTransaction
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int RecurringDate { get; set; }
    public RecurringTransactionType Type { get; set; }
    public int SourceId { get; set; }
    public int? TagId { get; set; }
    public int? GoalId { get; set; }
    public bool IsEnabled { get; set; }
    public SpendingSource Source { get; set; } = null!;
    public ExpenseTag? Tag { get; set; }
    public SavingGoal? Goal { get; set; }
}
