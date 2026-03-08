using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;

namespace Fluxo.ViewModels.Entities;

public partial class BudgetConfigVM : BaseEntityVM
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 12, ErrorMessage = "Month must be between 1 and 12.")]
    private int _month;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(2000, 2100, ErrorMessage = "Year must be between 2000 and 2100.")]
    private int _year;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 100, ErrorMessage = "Needs percentage must be between 0 and 100.")]
    [CustomValidation(typeof(BudgetConfigVM), nameof(ValidateBudgetSum))]
    [NotifyPropertyChangedFor(nameof(SumTotal))]
    [NotifyPropertyChangedFor(nameof(SumIsValid))]
    [NotifyPropertyChangedFor(nameof(SumWarning))]
    private decimal _needsPercentage = 50m;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 100, ErrorMessage = "Wants percentage must be between 0 and 100.")]
    [CustomValidation(typeof(BudgetConfigVM), nameof(ValidateBudgetSum))]
    [NotifyPropertyChangedFor(nameof(SumTotal))]
    [NotifyPropertyChangedFor(nameof(SumIsValid))]
    [NotifyPropertyChangedFor(nameof(SumWarning))]
    private decimal _wantsPercentage = 30m;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, 100, ErrorMessage = "Savings percentage must be between 0 and 100.")]
    [CustomValidation(typeof(BudgetConfigVM), nameof(ValidateBudgetSum))]
    [NotifyPropertyChangedFor(nameof(SumTotal))]
    [NotifyPropertyChangedFor(nameof(SumIsValid))]
    [NotifyPropertyChangedFor(nameof(SumWarning))]
    private decimal _savingsPercentage = 20m;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.UtcNow;

    // ── Derived helpers ───────────────────────────────────────────────────────

    public decimal SumTotal => NeedsPercentage + WantsPercentage + SavingsPercentage;

    public bool SumIsValid => Math.Abs(SumTotal - 100m) < 0.01m;

    /// <summary>
    /// Live feedback string shown beneath the percentage inputs.
    /// Green when valid, orange when close, red when over.
    /// </summary>
    public string SumWarning => SumIsValid
        ? "✓ Allocations add up to 100%"
        : $"Allocations total {SumTotal:F1}% — must equal 100%";

    /// <summary>
    /// Convenience: returns the month/year as a display label, e.g. "March 2025".
    /// </summary>
    public string MonthYearLabel =>
        Month is >= 1 and <= 12 && Year > 0
            ? new DateTime(Year, Month, 1).ToString("MMMM yyyy")
            : string.Empty;

    public BudgetConfigVM()
    {
        Month = DateTime.Today.Month;
        Year = DateTime.Today.Year;
        ValidateAllProperties();
    }

    // Cross-validate the other two percentages whenever any one changes.
    partial void OnNeedsPercentageChanged(decimal value)
    {
        ValidateProperty(WantsPercentage, nameof(ViewModels.Entities.BudgetConfigVM.WantsPercentage));
        ValidateProperty(SavingsPercentage, nameof(ViewModels.Entities.BudgetConfigVM.SavingsPercentage));
    }

    partial void OnWantsPercentageChanged(decimal value)
    {
        ValidateProperty(NeedsPercentage, nameof(ViewModels.Entities.BudgetConfigVM.NeedsPercentage));
        ValidateProperty(SavingsPercentage, nameof(ViewModels.Entities.BudgetConfigVM.SavingsPercentage));
    }

    partial void OnSavingsPercentageChanged(decimal value)
    {
        ValidateProperty(NeedsPercentage, nameof(ViewModels.Entities.BudgetConfigVM.NeedsPercentage));
        ValidateProperty(WantsPercentage, nameof(ViewModels.Entities.BudgetConfigVM.WantsPercentage));
    }

    // ── Custom validators ─────────────────────────────────────────────────────

    public static ValidationResult? ValidateBudgetSum(decimal _, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is not BudgetConfigVM vm) return ValidationResult.Success;
        var sum = vm.NeedsPercentage + vm.WantsPercentage + vm.SavingsPercentage;
        if (Math.Abs(sum - 100m) >= 0.01m)
            return new ValidationResult($"Needs + Wants + Savings must equal 100% (currently {sum:F1}%).");
        return ValidationResult.Success;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    public static BudgetConfigVM FromModel(BudgetConfig model) => new()
    {
        Id = model.Id,
        Month = model.Month,
        Year = model.Year,
        NeedsPercentage = model.NeedsPercentage,
        WantsPercentage = model.WantsPercentage,
        SavingsPercentage = model.SavingsPercentage,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt
    };

    /// <summary>
    /// Convenience factory that pre-fills with today's month/year and the
    /// canonical 50/30/20 defaults — ready to display in the settings popup.
    /// </summary>
    public static BudgetConfigVM Default() => new()
    {
        Month = DateTime.Today.Month,
        Year = DateTime.Today.Year,
        NeedsPercentage = 50m,
        WantsPercentage = 30m,
        SavingsPercentage = 20m
    };

    public BudgetConfig ToModel() => new()
    {
        Id = Id,
        Month = Month,
        Year = Year,
        NeedsPercentage = NeedsPercentage,
        WantsPercentage = WantsPercentage,
        SavingsPercentage = SavingsPercentage,
        CreatedAt = CreatedAt,
        UpdatedAt = DateTime.UtcNow
    };
}
