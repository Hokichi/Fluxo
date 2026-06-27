namespace Fluxo.Core.Entities;

public sealed class ExpenseLog
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public int AccountId { get; set; }
    public int? ParentLogId { get; set; }
    public Expense Expense { get; set; }
    public Account Account { get; set; }
    public ExpenseLog? ParentLog { get; set; }
    public decimal Amount { get; set; }
    public DateTime DeductedOn { get; set; }
    public string Notes { get; set; }
    public bool IsForDeletion { get; set; }
    public bool IsPinned { get; set; }
    public bool IsIoU { get; set; }
    public bool IsExcludedFromBudget { get; set; }
}
