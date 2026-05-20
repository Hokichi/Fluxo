using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsFixedExpenseItemVM : ObservableObject
{
    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private bool _isSelected;

    public SettingsFixedExpenseItemVM(RecurringTransaction recurringTransaction)
    {
        Id = recurringTransaction.Id;
        Name = recurringTransaction.Name;
        Amount = recurringTransaction.Amount;
        TagName = recurringTransaction.Tag?.Name ?? "Untagged";
        SpendingSourceName = recurringTransaction.Source?.Name ?? "No source";
        RecurringPeriod = recurringTransaction.RecurringPeriod;
        RecurringTime = recurringTransaction.RecurringTime;
        IsEnabled = recurringTransaction.IsEnabled;
        IsHidden = false;
        Type = recurringTransaction.Type;
    }

    public int Id { get; }
    public string Name { get; }
    public decimal Amount { get; }
    public string TagName { get; }
    public string SpendingSourceName { get; }
    public RecurringPeriod RecurringPeriod { get; }
    public int RecurringTime { get; }
    public bool IsEnabled { get; }
    public bool IsHidden { get; }
    public RecurringTransactionType Type { get; }
}
