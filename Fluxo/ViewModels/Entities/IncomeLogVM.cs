using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Entities;

public partial class IncomeLogVM : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private SpendingSourceVM _spendingSource = new();
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private DateTime _addedOn;
    [ObservableProperty] private string _notes = string.Empty;
}
