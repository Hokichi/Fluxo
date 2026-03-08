using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;

namespace Fluxo.ViewModels.Entities;

public partial class SavingsAccountVM : BaseEntityVM
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Account name is required.")]
    [MaxLength(100, ErrorMessage = "Account name cannot exceed 100 characters.")]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, double.MaxValue, ErrorMessage = "Initial balance cannot be negative.")]
    private decimal _initialBalance;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, double.MaxValue, ErrorMessage = "Current balance cannot be negative.")]
    [NotifyPropertyChangedFor(nameof(GrowthAmount))]
    [NotifyPropertyChangedFor(nameof(GrowthPercent))]
    private decimal _currentBalance;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 100, ErrorMessage = "Annual interest rate must be between 0% and 100%.")]
    [NotifyPropertyChangedFor(nameof(MonthlyInterestRate))]
    [NotifyPropertyChangedFor(nameof(MonthlyInterestDisplay))]
    private decimal _annualInterestRate;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Start date is required.")]
    [CustomValidation(typeof(SavingsAccountVM), nameof(ValidateStartDate))]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    private string? _notes;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.UtcNow;

    // ── Derived helpers ───────────────────────────────────────────────────────

    /// <summary>Monthly equivalent of the annual rate, e.g. 6% → 0.5%.</summary>
    public decimal MonthlyInterestRate => AnnualInterestRate / 12m;

    /// <summary>Formatted monthly rate for the UI label, e.g. "0.50% / month".</summary>
    public string MonthlyInterestDisplay => $"{MonthlyInterestRate:F2}% / month";

    /// <summary>Absolute growth since the account was opened.</summary>
    public decimal GrowthAmount => CurrentBalance - InitialBalance;

    /// <summary>Percentage growth since opening. Returns 0 when initial balance is 0.</summary>
    public decimal GrowthPercent => InitialBalance == 0 ? 0m
        : Math.Round((CurrentBalance - InitialBalance) / InitialBalance * 100m, 2);

    /// <summary>
    /// Cached 12-month projected balance. Populated externally by the UI layer
    /// (via ISavingsService.ProjectAccountGrowthAsync) to avoid blocking the VM constructor.
    /// </summary>
    [ObservableProperty]
    private decimal _projectedBalanceIn12Months;

    public SavingsAccountVM()
    {
        ValidateAllProperties();
    }

    partial void OnInitialBalanceChanged(decimal value) => OnPropertyChanged(nameof(GrowthAmount));

    public static ValidationResult? ValidateStartDate(DateTime value, ValidationContext _)
    {
        if (value == default)
            return new ValidationResult("Start date is required.");
        if (value.Year < 2000)
            return new ValidationResult("Start date must be on or after year 2000.");
        return ValidationResult.Success;
    }

    public static SavingsAccountVM FromModel(SavingsAccount model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        InitialBalance = model.InitialBalance,
        CurrentBalance = model.CurrentBalance,
        AnnualInterestRate = model.AnnualInterestRate,
        StartDate = model.StartDate,
        IsActive = model.IsActive,
        Notes = model.Notes,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt
    };

    public SavingsAccount ToModel() => new()
    {
        Id = Id,
        Name = Name,
        InitialBalance = InitialBalance,
        CurrentBalance = CurrentBalance,
        AnnualInterestRate = AnnualInterestRate,
        StartDate = StartDate,
        IsActive = IsActive,
        Notes = Notes,
        CreatedAt = CreatedAt,
        UpdatedAt = DateTime.UtcNow
    };
}
