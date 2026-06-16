namespace Fluxo.Core.Entities;

public sealed class ExpenseLog
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public int SpendingSourceId { get; set; }
    public Expense Expense { get; set; }
    public SpendingSource SpendingSource { get; set; }
    public decimal Amount { get; set; }
    public DateTime DeductedOn { get; set; }
    public string Notes { get; set; }
    public bool IsForDeletion { get; set; }
    public bool IsPinned { get; set; }
}
