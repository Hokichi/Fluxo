using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using Fluxo.Core.Entities;

namespace Fluxo.ViewModels.Entities;

public partial class TagVM : BaseEntityVM
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Tag name is required.")]
    [MaxLength(50, ErrorMessage = "Tag name cannot exceed 50 characters.")]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "Tag color is required.")]
    [RegularExpression(@"^#([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$",
        ErrorMessage = "Color must be a valid hex code (e.g. #FF5733 or #FF5733FF).")]
    private string _color = "#808080";

    [ObservableProperty]
    private DateTime _createdAt = DateTime.UtcNow;

    /// <summary>
    /// How many expenses + fixed expenses currently use this tag.
    /// Populated by the UI layer before showing a delete confirmation.
    /// </summary>
    [ObservableProperty]
    private int _usageCount;

    /// <summary>True when deleting this tag would orphan existing expense associations.</summary>
    public bool HasUsages => UsageCount > 0;

    public TagVM() => ValidateAllProperties();

    partial void OnUsageCountChanged(int value) => OnPropertyChanged(nameof(HasUsages));

    public static TagVM FromModel(Tag model) => new()
    {
        Id = model.Id,
        Name = model.Name,
        Color = model.Color,
        CreatedAt = model.CreatedAt
    };

    public Tag ToModel() => new()
    {
        Id = Id,
        Name = Name,
        Color = Color,
        CreatedAt = CreatedAt
    };
}
