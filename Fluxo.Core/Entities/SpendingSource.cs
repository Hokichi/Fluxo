using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

public sealed class SpendingSource
{
    public int Id { get; set; }
    public string Name { get; set; }
    public SpendingSourceType SpendingSourceType { get; set; }
    public decimal AccountLimit { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal Balance { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? InterestRate { get; set; }
    public bool ShowOnUI { get; set; }
}