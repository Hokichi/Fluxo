using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class SpendingSourceVM : ObservableObject
{
    [ObservableProperty] private decimal _accountLimit;
    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private int _id;
    [ObservableProperty] private decimal? _interestRate;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private decimal _moneyIn;
    [ObservableProperty] private decimal _moneyOut;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _showOnUI;
    [ObservableProperty] private SpendingSourceType _spendingSourceType;
    [ObservableProperty] private decimal _spentAmount;

    public bool IsCashOrChecking =>
        SpendingSourceType is SpendingSourceType.Cash or SpendingSourceType.Checking;

    public bool IsCredit => SpendingSourceType == SpendingSourceType.Credit;

    public bool IsBnpl => SpendingSourceType == SpendingSourceType.BNPL;

    public bool IsSaving => SpendingSourceType == SpendingSourceType.Saving;

    public bool CanTransferOut => SpendingSourceType is not (SpendingSourceType.Credit or SpendingSourceType.BNPL);
    public bool CanTransfer => IsEnabled && CanTransferOut;

    public decimal PrimaryAmount => SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
        ? SpentAmount
        : Balance;

    public string PrimaryAmountLabel => SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL
        ? "Spent"
        : "Balance";

    public string TypeDisplayName => SpendingSourceType switch
    {
        SpendingSourceType.Credit => "Credit",
        SpendingSourceType.BNPL => "BNPL",
        SpendingSourceType.Checking => "Checking",
        SpendingSourceType.Cash => "Cash",
        SpendingSourceType.Saving => "Savings",
        _ => "Source"
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

    partial void OnShowOnUIChanged(bool value)
    {
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDisabled));
        OnPropertyChanged(nameof(CanTransfer));
    }

    partial void OnSpendingSourceTypeChanged(SpendingSourceType value)
    {
        OnPropertyChanged(nameof(IsCashOrChecking));
        OnPropertyChanged(nameof(IsCredit));
        OnPropertyChanged(nameof(IsBnpl));
        OnPropertyChanged(nameof(IsSaving));
        OnPropertyChanged(nameof(CanTransferOut));
        OnPropertyChanged(nameof(CanTransfer));
        OnPropertyChanged(nameof(PrimaryAmount));
        OnPropertyChanged(nameof(PrimaryAmountLabel));
        OnPropertyChanged(nameof(TypeDisplayName));
    }
}
