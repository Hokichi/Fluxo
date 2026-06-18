using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups.Helpers;

namespace Fluxo.ViewModels.Popups;

public enum GoalDeadlineActionType
{
    None = 0,
    MarkAsReached = 1,
    AbandonGoal = 2
}

public partial class GoalDeadlineActionVM : ObservableObject
{
    public GoalDeadlineActionVM(IEnumerable<AccountVM>? eligibleSources = null)
    {
        EligibleSourcesView = AccountComboBoxViewFactory.CreateGroupedByTypeThenName(
            EligibleSources,
            nameof(AccountVM.TypeDisplayName),
            nameof(AccountVM.AccountType),
            nameof(AccountVM.Name));

        if (eligibleSources is null)
            return;

        foreach (var source in eligibleSources
                     .Where(source => source.IsEnabled)
                     .OrderBy(source => source.AccountType)
                     .ThenBy(source => source.Name))
            EligibleSources.Add(source);

        SelectedSource = EligibleSources.FirstOrDefault();
    }

    [ObservableProperty] private decimal _enteredAmount;
    [ObservableProperty] private decimal _remainingAmount;
    [ObservableProperty] private AccountVM? _selectedSource;
    [ObservableProperty] private GoalDeadlineActionType _selectedAction = GoalDeadlineActionType.None;

    public ObservableCollection<AccountVM> EligibleSources { get; } = [];
    public ICollectionView EligibleSourcesView { get; }

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
