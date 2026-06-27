namespace Fluxo.Core.Entities;

public sealed class IncomeLog
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public Account Account { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime AddedOn { get; set; }
    public string Notes { get; set; }
    public bool IsForDeletion { get; set; }
    public bool IsPinned { get; set; }
    public bool IsIoU { get; set; }
    public bool IsExcludedFromBudget { get; set; }
}
