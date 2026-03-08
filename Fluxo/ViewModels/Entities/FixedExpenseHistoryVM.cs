using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;

namespace Fluxo.ViewModels.Entities;

/// <summary>
/// ViewModel for a single confirmed payment of a fixed expense.
/// Primarily used in history/audit views and to populate the variable-expense
/// "Enter amount this cycle" popup.
/// </summary>
public partial class FixedExpenseHistoryVM : BaseEntityVM
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, int.MaxValue, ErrorMessage = "A valid fixed expense must be selected.")]
    private int _fixedExpenseId;

    /// <summary>Display name of the parent FixedExpense — not persisted, UI only.</summary>
    [ObservableProperty]
    private string _fixedExpenseName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    private decimal _amount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Paid date is required.")]
    [CustomValidation(typeof(FixedExpenseHistoryVM), nameof(ValidatePaidDate))]
    private DateTime _paidDate = DateTime.Today;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    private string? _notes;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    public FixedExpenseHistoryVM() => ValidateAllProperties();

    public static ValidationResult? ValidatePaidDate(DateTime value, ValidationContext ctx)
    {
        if (value == default)
            return new ValidationResult("Paid date is required.");
        if (value > DateTime.Today)
            return new ValidationResult("Paid date cannot be in the future.");
        return ValidationResult.Success;
    }

    public static FixedExpenseHistoryVM FromModel(FixedExpenseHistory model) => new()
    {
        Id = model.Id,
        FixedExpenseId = model.FixedExpenseId,
        FixedExpenseName = model.FixedExpense?.Name ?? string.Empty,
        Amount = model.Amount,
        PaidDate = model.PaidDate,
        Notes = model.Notes,
        CreatedAt = model.CreatedAt
    };

    public FixedExpenseHistory ToModel() => new()
    {
        Id = Id,
        FixedExpenseId = FixedExpenseId,
        Amount = Amount,
        PaidDate = PaidDate,
        Notes = Notes,
        CreatedAt = CreatedAt
    };
}
