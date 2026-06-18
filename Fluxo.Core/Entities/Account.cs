using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

public sealed class Account
{
    public int Id { get; set; }
    public string Name { get; set; }
    public AccountType AccountType { get; set; }
    public decimal AccountLimit { get; set; }
    public decimal MaximumSpending { get; set; }
    public decimal? MinimumPayment { get; set; }
    public decimal SpentAmount { get; set; }
    public decimal Balance { get; set; }
    public int? MonthlyDueDate { get; set; }
    public int? DeductSource { get; set; }
    public decimal? InterestRate { get; set; }
    public bool PinnedOnUI { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsForDeletion { get; set; }
}
