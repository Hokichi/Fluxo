using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public class SpendingSourceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public SpendingSourceType SpendingSourceType { get; set; }
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

    /// <summary>Not mapped from entity — populated by service from IncomeLog aggregates.</summary>
    public decimal MoneyIn { get; set; }

    /// <summary>Not mapped from entity — populated by service from ExpenseLog aggregates.</summary>
    public decimal MoneyOut { get; set; }
}
