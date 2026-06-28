using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Popups;

public partial class NotificationChecklistActionItemVM : ObservableObject
{
    [ObservableProperty] private int _entityId;
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private NotificationChecklistItemActionType _selectedAction = NotificationChecklistItemActionType.Ignore;
    [ObservableProperty] private int? _selectedSourceId;
    [ObservableProperty] private bool _requiresSourceSelection;
    [ObservableProperty] private RecurringTransactionType? _recurringTransactionType;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private decimal _originalAmount;
    [ObservableProperty] private int? _selectedTagId;
    [ObservableProperty] private int? _selectedGoalId;
    [ObservableProperty] private bool _updateRecurringAmount;
    [ObservableProperty] private bool _isRepayment;

    public ObservableCollection<AccountVM> AvailableSources { get; } = [];
    public ObservableCollection<TagVM> AvailableTags { get; } = [];
    public ObservableCollection<SavingGoalVM> AvailableGoals { get; } = [];

    public bool IsRecurringTransaction => RecurringTransactionType.HasValue;
    public bool IsRecurringExpense => RecurringTransactionType == Core.Enums.RecurringTransactionType.Expense;
    public bool IsRecurringIncome => RecurringTransactionType == Core.Enums.RecurringTransactionType.Income;
    public bool IsRecurringGoalUpdate => RecurringTransactionType == Core.Enums.RecurringTransactionType.GoalUpdate;
    public bool AreRecurringFieldsEnabled => SelectedAction == NotificationChecklistItemActionType.Process;
    public bool ShowRecurringFields => IsRecurringTransaction && SelectedAction == NotificationChecklistItemActionType.Process;
    public bool ShowRepaymentFields => IsRepayment && SelectedAction == NotificationChecklistItemActionType.Process;
    public bool ShouldAskToUpdateAmount => AreRecurringFieldsEnabled && Amount != OriginalAmount;

    public bool IsIgnoreSelected
    {
        get => SelectedAction == NotificationChecklistItemActionType.Ignore;
        set
        {
            if (value)
                SelectedAction = NotificationChecklistItemActionType.Ignore;
        }
    }

    public bool IsPaidSelected
    {
        get => SelectedAction == NotificationChecklistItemActionType.Paid;
        set
        {
            if (value)
                SelectedAction = NotificationChecklistItemActionType.Paid;
        }
    }

    public bool IsProcessSelected
    {
        get => SelectedAction == NotificationChecklistItemActionType.Process;
        set
        {
            if (value)
                SelectedAction = NotificationChecklistItemActionType.Process;
        }
    }

    public bool ShowSourceSelector =>
        (RequiresSourceSelection && SelectedAction != NotificationChecklistItemActionType.Ignore) || IsRecurringTransaction;

    public bool IsSelected
    {
        get => SelectedAction != NotificationChecklistItemActionType.Ignore;
        set => SelectedAction = value
            ? NotificationChecklistItemActionType.Process
            : NotificationChecklistItemActionType.Ignore;
    }

    partial void OnSelectedActionChanged(NotificationChecklistItemActionType value)
    {
        OnPropertyChanged(nameof(IsIgnoreSelected));
        OnPropertyChanged(nameof(IsPaidSelected));
        OnPropertyChanged(nameof(IsProcessSelected));
        OnPropertyChanged(nameof(ShowSourceSelector));
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(AreRecurringFieldsEnabled));
        OnPropertyChanged(nameof(ShowRecurringFields));
        OnPropertyChanged(nameof(ShowRepaymentFields));
        OnPropertyChanged(nameof(ShouldAskToUpdateAmount));
    }

    partial void OnRequiresSourceSelectionChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSourceSelector));
    }

    partial void OnRecurringTransactionTypeChanged(RecurringTransactionType? value)
    {
        OnPropertyChanged(nameof(IsRecurringTransaction));
        OnPropertyChanged(nameof(IsRecurringExpense));
        OnPropertyChanged(nameof(IsRecurringIncome));
        OnPropertyChanged(nameof(IsRecurringGoalUpdate));
        OnPropertyChanged(nameof(ShowSourceSelector));
        OnPropertyChanged(nameof(ShowRecurringFields));
    }

    partial void OnAmountChanged(decimal value) => OnPropertyChanged(nameof(ShouldAskToUpdateAmount));
    partial void OnOriginalAmountChanged(decimal value) => OnPropertyChanged(nameof(ShouldAskToUpdateAmount));
    partial void OnIsRepaymentChanged(bool value) => OnPropertyChanged(nameof(ShowRepaymentFields));
}
