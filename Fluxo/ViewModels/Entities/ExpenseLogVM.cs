using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Entities;

public partial class ExpenseLogVM : ObservableObject
{
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private DateTime _deductedOn;
    [ObservableProperty] private ExpenseVM _expense = new();
    [ObservableProperty] private int _id;
    [ObservableProperty] private bool _isForDeletion;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private SpendingSourceVM _spendingSource = new();
}