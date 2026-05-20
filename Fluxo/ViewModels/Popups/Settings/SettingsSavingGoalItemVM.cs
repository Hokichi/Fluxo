using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsSavingGoalItemVM : ObservableObject
{
    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private bool _isSelected;

    public SettingsSavingGoalItemVM(SavingGoal savingGoal, bool isEnabled)
    {
        Id = savingGoal.Id;
        Name = savingGoal.Name;
        CurrentAmount = savingGoal.CurrentAmount;
        TargetAmount = savingGoal.TargetAmount;
        SavingEndDate = savingGoal.SavingEndDate;
        RecurringPeriod = savingGoal.RecurringPeriod;
        IsHidden = false;
        IsEnabled = isEnabled;
    }

    public int Id { get; }
    public string Name { get; }
    public decimal CurrentAmount { get; }
    public decimal TargetAmount { get; }
    public DateTime? SavingEndDate { get; }
    public RecurringPeriod RecurringPeriod { get; }
    public bool IsHidden { get; }
    public bool IsEnabled { get; }
    public decimal ProgressRatio => TargetAmount <= 0 ? 0m : Math.Clamp(CurrentAmount / TargetAmount, 0m, 1m);
    public int ProgressPercentage => (int)Math.Round(ProgressRatio * 100m, MidpointRounding.AwayFromZero);
}
