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
            AccountType.Checking => "Checking",
            AccountType.Cash => "Cash",
            AccountType.Saving => "Savings",
            _ => "Account"
        };
        PrimaryAmount = account.AccountType == AccountType.Credit
            ? account.SpentAmount
            : account.Balance;
        PrimaryAmountLabel = account.AccountType == AccountType.Credit
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
