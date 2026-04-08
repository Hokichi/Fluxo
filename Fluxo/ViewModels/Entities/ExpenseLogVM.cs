using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Entities;

public partial class ExpenseLogVM : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private ExpenseVM _expense = new();
    [ObservableProperty] private SpendingSourceVM _spendingSource = new();
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private DateTime _deductedOn;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private bool _isForDeletion;
}
