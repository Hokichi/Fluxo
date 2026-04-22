using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Entities;

public partial class SavingGoalVM : ObservableObject
{
    [ObservableProperty] private DateTime _createdOn;
    [ObservableProperty] private decimal _currentAmount;
    [ObservableProperty] private decimal _remainingAmount;
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private DateTime _savingEndDate;
    [ObservableProperty] private decimal _targetAmount;

    partial void OnCurrentAmountChanged(decimal oldValue, decimal newValue)
    {
        if (TargetAmount == 0)
            return;

        RemainingAmount = TargetAmount - CurrentAmount;
    }

    partial void OnTargetAmountChanged(decimal oldValue, decimal newValue)
    {
        if (TargetAmount == 0)
            return;

        RemainingAmount = TargetAmount - CurrentAmount;
    }
}