using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class SpendingSourceVM : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private SpendingSourceType _spendingSourceType;
    [ObservableProperty] private decimal _limit;
    [ObservableProperty] private decimal _spentAmount;
    [ObservableProperty] private decimal _balance;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private decimal? _interestRate;
}
