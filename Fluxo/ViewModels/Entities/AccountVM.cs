using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class AccountVM : ObservableObject
{
    [ObservableProperty] private decimal _accountLimit;
    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private int? _deductSource;
    [ObservableProperty] private int _id;
    [ObservableProperty] private decimal? _interestRate;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isDefault;
    [ObservableProperty] private decimal _maximumSpending;
    [ObservableProperty] private decimal? _minimumPayment;
    [ObservableProperty] private int? _monthlyDueDate;
    [ObservableProperty] private decimal _moneyIn;
    [ObservableProperty] private decimal _moneyOut;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _pinnedOnUI;
    [ObservableProperty] private AccountType _accountType;
    [ObservableProperty] private decimal _spentAmount;
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private decimal _difference;
    [ObservableProperty] private bool _isOverdue;

    public bool IsCashOrChecking =>
        AccountType is AccountType.Cash or AccountType.Checking;

    public bool IsCredit => AccountType == AccountType.Credit;
    public bool CanRepay => IsCredit;

    public bool IsSaving => AccountType == AccountType.Saving;

    public bool CanTransferOut => AccountType != AccountType.Credit;
    public bool CanTransfer => IsEnabled && CanTransferOut;
    public bool CanReconcile => AccountType != AccountType.Saving;

    public decimal PrimaryAmount => AccountType == AccountType.Credit
        ? SpentAmount
        : Balance;

    public string PrimaryAmountLabel => AccountType == AccountType.Credit
        ? "Spent"
        : "Balance";

    public string TypeDisplayName => AccountType switch
    {
        AccountType.Credit => "Credit",
        AccountType.Checking => "Checking",
        AccountType.Cash => "Cash",
        AccountType.Saving => "Savings",
        _ => "Account"
    };

    public bool IsDisabled => !IsEnabled;

    partial void OnBalanceChanged(decimal value)
    {
        OnPropertyChanged(nameof(PrimaryAmount));
    }

    partial void OnSpentAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(PrimaryAmount));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDisabled));
        OnPropertyChanged(nameof(CanTransfer));
    }

    partial void OnAccountTypeChanged(AccountType value)
    {
        OnPropertyChanged(nameof(IsCashOrChecking));
        OnPropertyChanged(nameof(IsCredit));
        OnPropertyChanged(nameof(CanRepay));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(CanTransferOut));
        OnPropertyChanged(nameof(CanTransfer));
        OnPropertyChanged(nameof(CanReconcile));
        OnPropertyChanged(nameof(PrimaryAmount));
        OnPropertyChanged(nameof(PrimaryAmountLabel));
        OnPropertyChanged(nameof(TypeDisplayName));
    }
}
