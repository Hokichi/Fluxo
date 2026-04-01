using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class ExpenseVM : ObservableObject
{
    [ObservableProperty] private int _id;
    [ObservableProperty] private SpendingSourceVM _spendingSource = new();
    [ObservableProperty] private ExpenseTagVM _expenseTag = new();
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private ExpenseKind _expenseKind;
    [ObservableProperty] private ExpenseCategory _expenseCategory;
    [ObservableProperty] private DateTime? _recurringDate;
    [ObservableProperty] private bool _isActive;
}
