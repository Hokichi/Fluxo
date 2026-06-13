using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Entities;

public partial class ExpenseTagVM : ObservableObject
{
    [ObservableProperty] private string _hexCode = string.Empty;
    [ObservableProperty] private int _id;
    [ObservableProperty] private bool _isSystemTag = false;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal? _spendingLimit;
}
