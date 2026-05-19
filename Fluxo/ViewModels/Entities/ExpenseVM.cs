using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class ExpenseVM : ObservableObject
{
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private ExpenseCategory _expenseCategory;
    [ObservableProperty] private ExpenseTagVM _expenseTag = new();
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private SpendingSourceVM _spendingSource = new();
}
