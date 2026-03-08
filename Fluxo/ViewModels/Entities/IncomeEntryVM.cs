using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;

namespace Fluxo.ViewModels.Entities;

public partial class IncomeEntryVM : BaseEntityVM
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, int.MaxValue, ErrorMessage = "A valid income source must be selected.")]
    private int _incomeSourceId;

    /// <summary>Display name sourced from the selected IncomeSource — not persisted, UI only.</summary>
    [ObservableProperty]
    private string _incomeSourceName = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Amount is required.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
    private decimal _amount;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Date is required.")]
    private DateTime _date = DateTime.Today;

    /// <summary>
    /// When false the date was auto-set to the configured DefaultEntryDay.
    /// Shown as a subtle visual cue in the UI so the user knows it was automatic.
    /// </summary>
    [ObservableProperty]
    private bool _isManualDate;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    private string? _notes;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    public IncomeEntryVM() => ValidateAllProperties();

    public static IncomeEntryVM FromModel(IncomeEntry model) => new()
    {
        Id = model.Id,
        IncomeSourceId = model.IncomeSourceId,
        IncomeSourceName = model.IncomeSource?.Name ?? string.Empty,
        Amount = model.Amount,
        Date = model.Date,
        IsManualDate = model.IsManualDate,
        Notes = model.Notes,
        CreatedAt = model.CreatedAt
    };

    public IncomeEntry ToModel() => new()
    {
        Id = Id,
        IncomeSourceId = IncomeSourceId,
        Amount = Amount,
        Date = Date,
        IsManualDate = IsManualDate,
        Notes = Notes,
        CreatedAt = CreatedAt
    };
}
