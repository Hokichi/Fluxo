using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;
using System.Globalization;

namespace Fluxo.ViewModels.Shell.Main;

public sealed partial class LedgerTransactionItemVM : ObservableObject
{
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private int _spendingSourceId;
    [ObservableProperty] private int _tagId;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _spendingSourceName = string.Empty;
    [ObservableProperty] private string _tagHexCode = string.Empty;
    [ObservableProperty] private string _tagName = string.Empty;

    public int Id { get; init; }
    public LedgerTransactionKind Kind { get; init; }
    public DateTime OccurredOn { get; init; }
    public ExpenseCategory? Category { get; init; }
    public bool IsGoal { get; init; }
    public bool IsRecurring { get; init; }

    public decimal SignedAmount => Kind == LedgerTransactionKind.Income ? Amount : -Amount;
    public string TypeLabel => Kind == LedgerTransactionKind.Income ? "Incomes" : "Expenses";
    public string CategoryLabel => Category switch
    {
        ExpenseCategory.Needs => "Needs",
        ExpenseCategory.Wants => "Wants",
        ExpenseCategory.Savings => "Invest",
        _ => "Uncategorized"
    };

    public string DateGroupKey => OccurredOn.ToString("MMM dd", CultureInfo.InvariantCulture).ToUpperInvariant();
    public string TagGroupKey => string.IsNullOrWhiteSpace(TagName) ? "Untagged" : TagName;
    public string SpendingSourceGroupKey => string.IsNullOrWhiteSpace(SpendingSourceName) ? "No source" : SpendingSourceName;
    public string TypeGroupKey => TypeLabel;
    public string CategoryGroupKey => CategoryLabel;
}
