using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fluxo.Core.Entities;

[Table("Tags")]
public class Tag
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Hex colour string, e.g. "#FF5733".</summary>
    [MaxLength(9)]
    public string Color { get; set; } = "#808080";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ExpenseTag> ExpenseTags { get; set; } = [];

    public ICollection<FixedExpenseTag> FixedExpenseTags { get; set; } = [];
}