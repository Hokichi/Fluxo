using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class ExpenseVM : BaseEntityVM
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Description is required.")]
    [MaxLength(200, ErrorMessage = "Description cannot exceed 200 characters.")]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    private decimal _amount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Date is required.")]
    private DateTime _date = DateTime.Today;

    [ObservableProperty]
    private bool _isManualDate;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Category is required.")]
    private ExpenseCategory _category = ExpenseCategory.Wants;

    // ── BNPL ──────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowBnplFields))]
    [NotifyPropertyChangedFor(nameof(BnplSourceIdEffective))]
    private bool _isBnpl;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ExpenseVM), nameof(ValidateBnplSourceId))]
    private int? _bnplSourceId;

    /// <summary>Display name of the selected BNPL source — not persisted, UI only.</summary>
    [ObservableProperty]
    private string _bnplSourceName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(ExpenseVM), nameof(ValidateBnplSetAside))]
    private decimal? _bnplSetAsideAmount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 120, ErrorMessage = "Instalment count must be between 1 and 120.")]
    private int? _bnplInstallmentCount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    private string? _notes;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    // ── Tags ─────────────────────────────────────────────────────────────────

    /// <summary>Tags currently attached to this expense (fully populated for display).</summary>
    public ObservableCollection<TagVM> Tags { get; } = [];

    // ── Derived display helpers ───────────────────────────────────────────────

    /// <summary>Whether the BNPL detail fields should be visible in the UI.</summary>
    public bool ShowBnplFields => IsBnpl;

    /// <summary>
    /// The effective BnplSourceId — null when IsBnpl is off.
    /// Used by converters / templates to simplify XAML binding.
    /// </summary>
    public int? BnplSourceIdEffective => IsBnpl ? BnplSourceId : null;

    /// <summary>
    /// The grey "set-aside" label shown next to the income total.
    /// Defaults to the full Amount when IsBnpl is on and no override was entered.
    /// </summary>
    public decimal EffectiveBnplSetAside =>
        IsBnpl ? (BnplSetAsideAmount ?? Amount) : 0m;

    public ExpenseVM()
    {
        ValidateAllProperties();
    }

    // Re-validate BNPL fields whenever the IsBnpl toggle changes.
    partial void OnIsBnplChanged(bool value)
    {
        ValidateProperty(BnplSourceId, nameof(ViewModels.Entities.ExpenseVM.BnplSourceId));
        ValidateProperty(BnplSetAsideAmount, nameof(ViewModels.Entities.ExpenseVM.BnplSetAsideAmount));
        OnPropertyChanged(nameof(EffectiveBnplSetAside));
    }

    // Keep EffectiveBnplSetAside in sync when Amount changes.
    partial void OnAmountChanged(decimal value) => OnPropertyChanged(nameof(EffectiveBnplSetAside));

    partial void OnBnplSetAsideAmountChanged(decimal? value) => OnPropertyChanged(nameof(EffectiveBnplSetAside));

    // ── Custom validators ─────────────────────────────────────────────────────

    public static ValidationResult? ValidateBnplSourceId(int? value, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is not ExpenseVM vm) return ValidationResult.Success;
        if (vm.IsBnpl && (!value.HasValue || value <= 0))
            return new ValidationResult("A BNPL source must be selected for BNPL expenses.");
        return ValidationResult.Success;
    }

    public static ValidationResult? ValidateBnplSetAside(decimal? value, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is not ExpenseVM vm) return ValidationResult.Success;
        if (vm.IsBnpl && value.HasValue && value <= 0)
            return new ValidationResult("Set-aside amount must be greater than 0.");
        if (vm.IsBnpl && value.HasValue && value > vm.Amount)
            return new ValidationResult("Set-aside cannot exceed the expense amount.");
        return ValidationResult.Success;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    public static ExpenseVM FromModel(Expense model)
    {
        var vm = new ExpenseVM
        {
            Id = model.Id,
            Description = model.Description,
            Amount = model.Amount,
            Date = model.Date,
            IsManualDate = model.IsManualDate,
            Category = model.Category,
            IsBnpl = model.IsBnpl,
            BnplSourceId = model.BnplSourceId,
            BnplSourceName = model.BnplSource?.Name ?? string.Empty,
            BnplSetAsideAmount = model.BnplSetAsideAmount,
            BnplInstallmentCount = model.BnplInstallmentCount,
            Notes = model.Notes,
            CreatedAt = model.CreatedAt
        };

        foreach (var et in model.ExpenseTags)
            vm.Tags.Add(TagVM.FromModel(et.Tag));

        vm.ValidateAllProperties();
        return vm;
    }

    public Expense ToModel() => new()
    {
        Id = Id,
        Description = Description,
        Amount = Amount,
        Date = Date,
        IsManualDate = IsManualDate,
        Category = Category,
        IsBnpl = IsBnpl,
        BnplSourceId = IsBnpl ? BnplSourceId : null,
        BnplSetAsideAmount = IsBnpl ? (BnplSetAsideAmount ?? Amount) : null,
        BnplInstallmentCount = IsBnpl ? BnplInstallmentCount : null,
        Notes = Notes,
        CreatedAt = CreatedAt,
        ExpenseTags = Tags.Select(t => new ExpenseTag { TagId = t.Id }).ToList()
    };
}