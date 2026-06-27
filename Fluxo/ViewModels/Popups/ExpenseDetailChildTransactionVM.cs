using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Popups;

public sealed class ExpenseDetailChildTransactionVM
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public DateTime DeductedOn { get; init; }
    public ExpenseCategory Category { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string TagName { get; init; } = string.Empty;
    public string TagHexCode { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public bool IsIoU { get; init; }

    public string CategoryLabel => Category switch
    {
        ExpenseCategory.Needs => "Needs",
        ExpenseCategory.Wants => "Wants",
        ExpenseCategory.Savings => "Invest",
        _ => "Uncategorized"
    };
}
