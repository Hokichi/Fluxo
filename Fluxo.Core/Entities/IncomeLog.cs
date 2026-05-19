namespace Fluxo.Core.Entities;

public sealed class IncomeLog
{
    public int Id { get; set; }
    public int SpendingSourceId { get; set; }
    public SpendingSource SpendingSource { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime AddedOn { get; set; }
    public string Notes { get; set; }
}
