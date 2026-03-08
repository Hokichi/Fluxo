using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Entities;

public partial class IncomeSourceVM : BaseEntityVM
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Income source type is required.")]
    private IncomeSourceType _type = IncomeSourceType.Salary;

    [ObservableProperty]
    private bool _isActive = true;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [MaxLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
    private string? _notes;

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    public IncomeSourceVM() => ValidateAllProperties();

    public static IncomeSourceVM FromModel(IncomeSource model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Type = model.Type,
        IsActive = model.IsActive,
        Notes = model.Notes,
        CreatedAt = model.CreatedAt
    };

    public IncomeSource ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Type = Type,
        IsActive = IsActive,
        Notes = Notes,
        CreatedAt = CreatedAt
    };
}