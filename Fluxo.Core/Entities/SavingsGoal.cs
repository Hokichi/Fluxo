using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

[Table("SavingsGoals")]
public class SavingsGoal
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "REAL")]
    public decimal TargetAmount { get; set; }

    [Column(TypeName = "REAL")]
    public decimal CurrentAmount { get; set; } = 0;

    [Column(TypeName = "REAL")]
    public decimal ContributionAmount { get; set; }

    public ContributionFrequency ContributionFrequency { get; set; } = ContributionFrequency.Monthly;

    public DateTime StartDate { get; set; }

    /// <summary>True when the user explicitly chose StartDate instead of the auto default.</summary>
    public bool IsManualDate { get; set; } = false;

    /// <summary>
    /// Calculated and stored on save so queries don't need to recompute it.
    /// Recalculated whenever CurrentAmount or ContributionAmount changes.
    /// </summary>
    public DateTime? EstimatedCompletionDate { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsCompleted { get; set; } = false;
    public DateTime? CompletedDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}