using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class BnplSourceVM : BaseEntityVM
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "BNPL source type is required.")]
    private BnplSourceType _type = BnplSourceType.CreditCard;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0.01, double.MaxValue, ErrorMessage = "Credit limit must be greater than 0 when specified.")]
    private decimal? _creditLimit;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, double.MaxValue, ErrorMessage = "Current balance cannot be negative.")]
    private decimal _currentBalance;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    private string? _notes;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    /// <summary>
    /// Utilization % for display (e.g., "72%" credit card usage indicator).
    /// Null when no credit limit is set.
    /// </summary>
    public decimal? UtilizationPercent =>
        CreditLimit.HasValue && CreditLimit > 0
            ? Math.Min(100m, CurrentBalance / CreditLimit.Value * 100m)
            : null;

    /// <summary>True when the balance exceeds the credit limit.</summary>
    public bool IsOverLimit => CreditLimit.HasValue && CurrentBalance > CreditLimit.Value;

    public BnplSourceVM()
    {
        ValidateAllProperties();
    }

    partial void OnCurrentBalanceChanged(decimal value) => OnPropertyChanged(nameof(UtilizationPercent));

    partial void OnCreditLimitChanged(decimal? value) => OnPropertyChanged(nameof(UtilizationPercent));

    public static BnplSourceVM FromModel(BnplSource model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Type = model.Type,
        CreditLimit = model.CreditLimit,
        CurrentBalance = model.CurrentBalance,
        IsActive = model.IsActive,
        Notes = model.Notes,
        CreatedAt = model.CreatedAt
    };

    public BnplSource ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Type = Type,
        CreditLimit = CreditLimit,
        CurrentBalance = CurrentBalance,
        IsActive = IsActive,
        Notes = Notes,
        CreatedAt = CreatedAt
    };
}