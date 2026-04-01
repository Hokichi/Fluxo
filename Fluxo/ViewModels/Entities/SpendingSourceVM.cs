using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class SpendingSourceVM : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private SpendingSourceType _spendingSourceType;
    [ObservableProperty] private decimal _moneyIn;
    [ObservableProperty] private decimal _moneyOut;
    [ObservableProperty] private decimal _accountLimit;
    [ObservableProperty] private decimal _spentAmount;
    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private bool _showOnUI;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private decimal? _interestRate;
}
