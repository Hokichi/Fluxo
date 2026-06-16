using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Entities;

public partial class IncomeLogVM : ObservableObject
{
    [ObservableProperty] private DateTime _addedOn;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private int _id;
    [ObservableProperty] private bool _isPinned;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private SpendingSourceVM _spendingSource = new();
}
