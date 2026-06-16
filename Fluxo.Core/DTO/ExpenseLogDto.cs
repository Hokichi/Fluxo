namespace Fluxo.Core.DTO;

public class ExpenseLogDto
{
    public int Id { get; set; }
    public int ExpenseId { get; set; }
    public int SpendingSourceId { get; set; }
    public ExpenseDto Expense { get; set; } = new();
    public SpendingSourceDto SpendingSource { get; set; } = new();
    public decimal Amount { get; set; }
    public DateTime DeductedOn { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsForDeletion { get; set; }
    public bool IsPinned { get; set; }
}
