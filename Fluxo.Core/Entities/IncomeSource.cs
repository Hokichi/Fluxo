using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

/// <summary>
/// Represents a source of income (salary job, side gig, investment account, etc.).
/// A single source can have many income entries over time.
/// </summary>
[Table("IncomeSources")]
public class IncomeSource
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public IncomeSourceType Type { get; set; } = IncomeSourceType.Salary;

    /// <summary>
    /// Soft-delete flag. Inactive sources are hidden from the UI but kept for historical records.
    /// </summary>
    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<IncomeEntry> Entries { get; set; } = [];
}