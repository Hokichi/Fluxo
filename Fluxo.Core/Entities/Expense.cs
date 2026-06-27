using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

public sealed class Expense
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int TagId { get; set; }
    public Account Account { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; }
    public bool IsIoU { get; set; }
}
