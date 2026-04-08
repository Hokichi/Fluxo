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
    [ObservableProperty] private decimal _moneyIn;
    [ObservableProperty] private decimal _moneyOut;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _showOnUI;
    [ObservableProperty] private SpendingSourceType _spendingSourceType;
    [ObservableProperty] private decimal _spentAmount;
}