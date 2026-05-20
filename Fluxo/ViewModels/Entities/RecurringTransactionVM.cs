using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class RecurringTransactionVM : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private RecurringPeriod _recurringPeriod;
    [ObservableProperty] private int _recurringTime;
    [ObservableProperty] private RecurringTransactionType _type;
    [ObservableProperty] private SpendingSourceVM _source = new();
    [ObservableProperty] private ExpenseTagVM? _tag;
    [ObservableProperty] private SavingGoalVM? _goal;
    [ObservableProperty] private bool _isEnabled = true;
}
