using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsAccountItemVM : ObservableObject
{
    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private bool _isSelected;

    public SettingsAccountItemVM(Account account)
    {
        Id = account.Id;
        Name = account.Name;
        TypeDisplayName = account.AccountType switch
        {
            AccountType.Credit => "Credit",
            AccountType.BNPL => "BNPL",
            AccountType.Checking => "Checking",
            AccountType.Cash => "Cash",
            AccountType.Saving => "Savings",
            _ => "Source"
        };
        PrimaryAmount = account.AccountType is AccountType.Credit or AccountType.BNPL
            ? account.SpentAmount
            : account.Balance;
        PrimaryAmountLabel = account.AccountType is AccountType.Credit or AccountType.BNPL
            ? "Spent"
            : "Balance";
        MaximumSpending = account.MaximumSpending;
        MinimumPayment = account.MinimumPayment;
        IsEnabled = account.IsEnabled;
        IsUnpinned = !account.PinnedOnUI;
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
