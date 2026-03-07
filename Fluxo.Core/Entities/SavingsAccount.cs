using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fluxo.Core.Entities;

[Table("SavingsAccounts")]
public class SavingsAccount
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "REAL")]
    public decimal InitialBalance { get; set; } = 0;

    [Column(TypeName = "REAL")]
    public decimal CurrentBalance { get; set; } = 0;

    /// <summary>Annual interest rate as a percentage, e.g. 6.0 means 6 % p.a.</summary>
    [Column(TypeName = "REAL")]
    public decimal AnnualInterestRate { get; set; } = 0;

    public DateTime StartDate { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}