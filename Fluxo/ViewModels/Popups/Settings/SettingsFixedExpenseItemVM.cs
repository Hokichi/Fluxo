using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsFixedExpenseItemVM : ObservableObject
{
    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private bool _isSelected;

    public SettingsFixedExpenseItemVM(Expense expense)
    {
        Id = expense.Id;
        Name = expense.Name;
        Amount = expense.Amount;
        TagName = expense.ExpenseTag?.Name ?? "Untagged";
        SpendingSourceName = expense.SpendingSource?.Name ?? "No source";
        RecurringDate = expense.RecurringDate;
        IsEnabled = expense.IsActive;
        IsHidden = false;
    }

    public int Id { get; }
    public string Name { get; }
    public decimal Amount { get; }
    public string TagName { get; }
    public string SpendingSourceName { get; }
    public int? RecurringDate { get; }
    public bool IsEnabled { get; }
    public bool IsHidden { get; }
}
