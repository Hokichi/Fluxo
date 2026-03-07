using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

[Table("BnplSources")]
public class BnplSource
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public BnplSourceType Type { get; set; } = BnplSourceType.CreditCard;

    /// <summary>Maximum credit / limit on this BNPL account. Null = no limit tracked.</summary>
    [Column(TypeName = "REAL")]
    public decimal? CreditLimit { get; set; }

    /// <summary>Running balance currently owed on this source.</summary>
    [Column(TypeName = "REAL")]
    public decimal CurrentBalance { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Expense> Expenses { get; set; } = [];
}