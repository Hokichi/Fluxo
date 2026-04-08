using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Entities;

public partial class SavingGoalVM : ObservableObject
{
    [ObservableProperty] private decimal _currentAmount;
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private DateTime _savingEndDate;
    [ObservableProperty] private decimal _targetAmount;
}