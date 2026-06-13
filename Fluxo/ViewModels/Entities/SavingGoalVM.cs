using CommunityToolkit.Mvvm.ComponentModel;
using System.Globalization;

namespace Fluxo.ViewModels.Entities;

public partial class SavingGoalVM : ObservableObject
{
    [ObservableProperty] private DateTime _createdOn;
    [ObservableProperty] private decimal _currentAmount;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private decimal _remainingAmount;
    [ObservableProperty] private int _id;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private DateTime? _savingEndDate;
    [ObservableProperty] private decimal _targetAmount;

    public string AmountLeftText => FormatMoneyAmount(RemainingAmount);
    public string WeeklyAverageText => FormatMoneyAmount(Math.Ceiling(CurrentAmount / GetElapsedCompletedWeeks()));
    public string EstimatedDeadlineText => SavingEndDate?.ToString("MMM d, yyyy", CultureInfo.CurrentCulture) ?? "Undefined";

    partial void OnCurrentAmountChanged(decimal oldValue, decimal newValue)
    {
        if (TargetAmount == 0)
            return;

        RemainingAmount = TargetAmount - CurrentAmount;
        OnPropertyChanged(nameof(AmountLeftText));
        OnPropertyChanged(nameof(WeeklyAverageText));
    }

    partial void OnTargetAmountChanged(decimal oldValue, decimal newValue)
    {
        if (TargetAmount == 0)
            return;

        RemainingAmount = TargetAmount - CurrentAmount;
        OnPropertyChanged(nameof(AmountLeftText));
    }

    partial void OnCreatedOnChanged(DateTime value)
    {
        OnPropertyChanged(nameof(WeeklyAverageText));
    }

    partial void OnSavingEndDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(EstimatedDeadlineText));
    }

    private int GetElapsedCompletedWeeks()
    {
        var elapsedDays = Math.Max(0, (DateTime.Today - CreatedOn.Date).Days);
        return Math.Max(1, elapsedDays / 7);
    }

    private static string FormatMoneyAmount(decimal amount)
    {
        return amount.ToString("N0", CultureInfo.CurrentCulture);
    }
}
