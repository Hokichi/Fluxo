using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

public sealed class Expense
{
    public int Id { get; set; }
    public int SpendingSourceId { get; set; }
    public int ExpenseTagId { get; set; }
    public SpendingSource SpendingSource { get; set; } = null!;
    public ExpenseTag ExpenseTag { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; }
}
