using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fluxo.Core.Entities;

[Table("IncomeEntries")]
public class IncomeEntry
{
    [Key]
    public int Id { get; set; }

    public int IncomeSourceId { get; set; }

    [Column(TypeName = "REAL")]
    public decimal Amount { get; set; }

    public DateTime Date { get; set; }

    /// <summary>True when the user explicitly set the date rather than accepting the auto default.</summary>
    public bool IsManualDate { get; set; } = false;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(IncomeSourceId))]
    public IncomeSource IncomeSource { get; set; } = null!;
}