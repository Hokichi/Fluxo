using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class FixedExpenseVM : BaseEntityVM
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Amount mode is required.")]
    [NotifyPropertyChangedFor(nameof(IsFixedMode))]
    [NotifyPropertyChangedFor(nameof(IsVariableMode))]
    [NotifyPropertyChangedFor(nameof(AmountPlaceholder))]
    private FixedExpenseAmountMode _amountMode = FixedExpenseAmountMode.Fixed;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [CustomValidation(typeof(FixedExpenseVM), nameof(ValidateAmount))]
    private decimal? _amount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Due day is required.")]
    [Range(1, 28, ErrorMessage = "Due day must be between 1 and 28.")]
    private int _dueDay = 1;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Category is required.")]
    private ExpenseCategory _category = ExpenseCategory.Needs;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    private bool _notificationEnabled = true;

    /// <summary>
    /// Date the expense was last marked as paid.
    /// Null means it has never been confirmed this cycle.
    /// </summary>
    [ObservableProperty]
    private DateTime? _lastPaidDate;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    private string? _notes;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    // ── Tags ─────────────────────────────────────────────────────────────────

    public ObservableCollection<TagVM> Tags { get; } = [];

    // ── Derived display helpers ───────────────────────────────────────────────

    public bool IsFixedMode => AmountMode == FixedExpenseAmountMode.Fixed;
    public bool IsVariableMode => AmountMode == FixedExpenseAmountMode.Variable;

    /// <summary>Placeholder text for the amount field based on current mode.</summary>
    public string AmountPlaceholder => IsVariableMode
        ? "Entered each cycle"
        : "Fixed amount";

    /// <summary>
    /// True when the bill is due today or overdue in the current month.
    /// Intended for dashboard badge / highlight logic.
    /// </summary>
    public bool IsDueOrOverdue
    {
        get
        {
            var today = DateTime.Today;
            if (LastPaidDate.HasValue &&
                LastPaidDate.Value.Month == today.Month &&
                LastPaidDate.Value.Year == today.Year)
                return false;

            var clampedDay = Math.Min((int)DueDay, DateTime.DaysInMonth(today.Year, today.Month));
            return today.Day >= clampedDay;
        }
    }

    public FixedExpenseVM()
    {
        ValidateAllProperties();
    }

    // Re-validate Amount whenever the mode switches.
    partial void OnAmountModeChanged(FixedExpenseAmountMode value)
        => ValidateProperty(Amount, nameof(ViewModels.Entities.FixedExpenseVM.Amount));

    // ── Custom validators ─────────────────────────────────────────────────────

    public static ValidationResult? ValidateAmount(decimal? value, ValidationContext ctx)
    {
        if (ctx.ObjectInstance is not FixedExpenseVM vm) return ValidationResult.Success;

        if (vm.AmountMode == FixedExpenseAmountMode.Fixed)
        {
            if (!value.HasValue)
                return new ValidationResult("Amount is required for fixed expenses.");
            if (value <= 0)
                return new ValidationResult("Amount must be greater than 0.");
        }

        return ValidationResult.Success;
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    public static FixedExpenseVM FromModel(FixedExpense model)
    {
        var vm = new FixedExpenseVM
        {
            Id = model.Id,
            Name = model.Name,
            AmountMode = model.AmountMode,
            Amount = model.Amount,
            DueDay = model.DueDay,
            Category = model.Category,
            IsActive = model.IsActive,
            NotificationEnabled = model.NotificationEnabled,
            LastPaidDate = model.LastPaidDate,
            Notes = model.Notes,
            CreatedAt = model.CreatedAt
        };

        foreach (var ft in model.FixedExpenseTags)
            vm.Tags.Add(TagVM.FromModel(ft.Tag));

        vm.ValidateAllProperties();
        return vm;
    }

    public FixedExpense ToModel() => new()
    {
        Id = Id,
        Name = Name,
        AmountMode = AmountMode,
        Amount = IsFixedMode ? Amount : null,
        DueDay = DueDay,
        Category = Category,
        IsActive = IsActive,
        NotificationEnabled = NotificationEnabled,
        LastPaidDate = LastPaidDate,
        Notes = Notes,
        CreatedAt = CreatedAt,
        FixedExpenseTags = Tags.Select(t => new FixedExpenseTag { TagId = t.Id }).ToList()
    };
}