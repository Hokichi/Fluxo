using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups;

public enum GoalDeadlineActionType
{
    None = 0,
    MarkAsReached = 1,
    AbandonGoal = 2
}

public partial class GoalDeadlineActionVM : ObservableObject
{
    public GoalDeadlineActionVM(IEnumerable<SpendingSourceVM>? eligibleSources = null)
    {
        if (eligibleSources is null)
            return;

        foreach (var source in eligibleSources)
            EligibleSources.Add(source);

        SelectedSource = EligibleSources.FirstOrDefault();
    }

    [ObservableProperty] private decimal _enteredAmount;
    [ObservableProperty] private decimal _remainingAmount;
    [ObservableProperty] private SpendingSourceVM? _selectedSource;
    [ObservableProperty] private GoalDeadlineActionType _selectedAction = GoalDeadlineActionType.None;

    public ObservableCollection<SpendingSourceVM> EligibleSources { get; } = [];

    public bool CanMarkAsReached => EnteredAmount != RemainingAmount;

    partial void OnEnteredAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(CanMarkAsReached));
        MarkAsReachedCommand.NotifyCanExecuteChanged();
    }

    partial void OnRemainingAmountChanged(decimal value)
    {
        OnPropertyChanged(nameof(CanMarkAsReached));
        MarkAsReachedCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanMarkAsReached))]
    private void MarkAsReached()
    {
        SelectedAction = GoalDeadlineActionType.MarkAsReached;
    }

    [RelayCommand]
    private void AbandonGoal()
    {
        SelectedAction = GoalDeadlineActionType.AbandonGoal;
    }
}
