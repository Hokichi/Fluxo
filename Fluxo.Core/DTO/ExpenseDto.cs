using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public class ExpenseDto
{
    public int Id { get; set; }
    public int SpendingSourceId { get; set; }
    public int ExpenseTagId { get; set; }
    public SpendingSourceDto SpendingSource { get; set; } = new();
    public ExpenseTagDto ExpenseTag { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public ExpenseKind ExpenseKind { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; }
    public int? RecurringDate { get; set; }
    public bool IsActive { get; set; }
}
