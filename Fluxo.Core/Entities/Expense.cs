using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

public sealed class Expense
{
    public int Id { get; set; }
    public SpendingSource SpendingSource { get; set; }
    public ExpenseTag ExpenseTag { get; set; }
    public string Name { get; set; }
    public decimal Amount { get; set; }
    public ExpenseKind ExpenseKind { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; }
    public DateTime? RecurringDate { get; set; }
    public bool IsActive { get; set; }
}