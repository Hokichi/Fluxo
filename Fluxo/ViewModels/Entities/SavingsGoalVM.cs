using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class SavingsGoalVM : BaseEntityVM
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Goal name is required.")]
    [MaxLength(100, ErrorMessage = "Goal name cannot exceed 100 characters.")]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Target amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Target amount must be greater than 0.")]
    [NotifyPropertyChangedFor(nameof(RemainingAmount))]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    [CustomValidation(typeof(SavingsGoalVM), nameof(ValidateTargetAmount))]
    private decimal _targetAmount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, double.MaxValue, ErrorMessage = "Current amount cannot be negative.")]
    [NotifyPropertyChangedFor(nameof(RemainingAmount))]
    [NotifyPropertyChangedFor(nameof(ProgressPercent))]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    private decimal _currentAmount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Contribution amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Contribution must be greater than 0.")]
    [NotifyPropertyChangedFor(nameof(ViewModels.Entities.SavingsGoalVM.EstimatedCompletionDate))]
    private decimal _contributionAmount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Contribution frequency is required.")]
    [NotifyPropertyChangedFor(nameof(FrequencyLabel))]
    [NotifyPropertyChangedFor(nameof(ViewModels.Entities.SavingsGoalVM.EstimatedCompletionDate))]
    private ContributionFrequency _contributionFrequency = ContributionFrequency.Monthly;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Start date is required.")]
    [CustomValidation(typeof(SavingsGoalVM), nameof(ValidateStartDate))]
    [NotifyPropertyChangedFor(nameof(ViewModels.Entities.SavingsGoalVM.EstimatedCompletionDate))]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private bool _isManualDate;

    /// <summary>
    /// Calculated and stored by ISavingsService when the goal is saved or updated.
    /// Exposed here as observable so the UI auto-updates on ContributionAmount / Frequency change.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DaysRemaining))]
    [NotifyPropertyChangedFor(nameof(IsOnTrack))]
    private DateTime? _estimatedCompletionDate;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    private bool _isCompletedFlag;

    [ObservableProperty]
    private DateTime? _completedDate;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    private string? _notes;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.UtcNow;

    // ── Derived helpers ───────────────────────────────────────────────────────

    public decimal RemainingAmount => Math.Max(0m, TargetAmount - CurrentAmount);

    public decimal ProgressPercent => TargetAmount == 0 ? 100m
        : Math.Min(100m, Math.Round(CurrentAmount / TargetAmount * 100m, 1));

    public bool IsCompleted => IsCompletedFlag || CurrentAmount >= TargetAmount;

    public int? DaysRemaining => EstimatedCompletionDate.HasValue && !IsCompleted
        ? Math.Max(0, (EstimatedCompletionDate.Value.Date - DateTime.Today).Days)
        : null;

    public bool IsOnTrack => EstimatedCompletionDate.HasValue && !IsCompleted
        && EstimatedCompletionDate.Value >= DateTime.Today;

    public string FrequencyLabel => ContributionFrequency switch
    {
        ContributionFrequency.Daily => "Daily",
        ContributionFrequency.Weekly => "Weekly",
        ContributionFrequency.BiWeekly => "Every 2 weeks",
        ContributionFrequency.Monthly => "Monthly",
        ContributionFrequency.BiMonthly => "Every 2 months",
        ContributionFrequency.Quarterly => "Quarterly",
        ContributionFrequency.BiQuarterly => "Every 6 months",
        ContributionFrequency.Annually => "Annually",
        _ => ContributionFrequency.ToString()
    };

    public SavingsGoalVM()
    {
        ValidateAllProperties();
    }

    // ── Custom validators ─────────────────────────────────────────────────────

    public static ValidationResult? ValidateTargetAmount(decimal value, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is not SavingsGoalVM vm) return ValidationResult.Success;
        if (value > 0 && vm.CurrentAmount > value)
            return new ValidationResult("Target amount cannot be less than the current amount.");
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateStartDate(DateTime value, ValidationContext _)
    {
        if (value == default)
            return new ValidationResult("Start date is required.");
        if (value.Year < 2000)
            return new ValidationResult("Start date must be on or after year 2000.");
        return ValidationResult.Success;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    public static SavingsGoalVM FromModel(SavingsGoal model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        TargetAmount = model.TargetAmount,
        CurrentAmount = model.CurrentAmount,
        ContributionAmount = model.ContributionAmount,
        ContributionFrequency = model.ContributionFrequency,
        StartDate = model.StartDate,
        IsManualDate = model.IsManualDate,
        EstimatedCompletionDate = model.EstimatedCompletionDate,
        IsActive = model.IsActive,
        IsCompletedFlag = model.IsCompleted,
        CompletedDate = model.CompletedDate,
        Notes = model.Notes,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt
    };

    public SavingsGoal ToModel() => new()
    {
        Id = Id,
        Name = Name,
        TargetAmount = TargetAmount,
        CurrentAmount = CurrentAmount,
        ContributionAmount = ContributionAmount,
        ContributionFrequency = ContributionFrequency,
        StartDate = StartDate,
        IsManualDate = IsManualDate,
        EstimatedCompletionDate = EstimatedCompletionDate,
        IsActive = IsActive,
        IsCompleted = IsCompleted,
        CompletedDate = CompletedDate,
        Notes = Notes,
        CreatedAt = CreatedAt,
        UpdatedAt = DateTime.UtcNow
    };
}