namespace Fluxo.ViewModels.Entities;

public partial class SavingGoalVM
{
    public decimal ProgressRatio => TargetAmount <= 0 ? 0m : Math.Clamp(CurrentAmount / TargetAmount, 0m, 1m);

    public int ProgressPercentage => (int)Math.Round(ProgressRatio * 100, MidpointRounding.AwayFromZero);

    partial void OnCurrentAmountChanged(decimal value)
    {
        NotifyProgressChanged();
    }

    partial void OnTargetAmountChanged(decimal value)
    {
        NotifyProgressChanged();
    }

    private void NotifyProgressChanged()
    {
        OnPropertyChanged(nameof(ProgressRatio));
        OnPropertyChanged(nameof(ProgressPercentage));
    }
}
