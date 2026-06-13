using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsSpendingSourceItemVM : ObservableObject
{
    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private bool _isSelected;

    public SettingsSpendingSourceItemVM(SpendingSource spendingSource)
    {
        Id = spendingSource.Id;
        Name = spendingSource.Name;
        TypeDisplayName = spendingSource.SpendingSourceType switch
        {
            SpendingSourceType.Credit => "Credit",
            SpendingSourceType.BNPL => "BNPL",
            SpendingSourceType.Checking => "Checking",
            SpendingSourceType.Cash => "Cash",
            SpendingSourceType.Saving => "Savings",
            _ => "Source"
        };
        PrimaryAmount = spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? spendingSource.SpentAmount
            : spendingSource.Balance;
        PrimaryAmountLabel = spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
            ? "Spent"
            : "Balance";
        MaximumSpending = spendingSource.MaximumSpending;
        MinimumPayment = spendingSource.MinimumPayment;
        IsEnabled = spendingSource.IsEnabled;
        IsUnpinned = !spendingSource.PinnedOnUI;
    }

    public int Id { get; }
    public string Name { get; }
    public string TypeDisplayName { get; }
    public decimal PrimaryAmount { get; }
    public string PrimaryAmountLabel { get; }
    public decimal MaximumSpending { get; }
    public decimal? MinimumPayment { get; }
    public bool IsEnabled { get; }
    public bool IsUnpinned { get; }
}
