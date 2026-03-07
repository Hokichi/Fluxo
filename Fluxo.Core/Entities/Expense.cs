using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

[Table("Expenses")]
public class Expense
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "REAL")]
    public decimal Amount { get; set; }

    public DateTime Date { get; set; }

    /// <summary>True when the user explicitly chose the date.</summary>
    public bool IsManualDate { get; set; } = false;

    /// <summary>Which 50/30/20 bucket this expense belongs to.</summary>
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Wants;

    // ── BNPL fields ────────────────────────────────────────────────────────────
    public bool IsBnpl { get; set; } = false;

    public int? BnplSourceId { get; set; }

    /// <summary>
    /// Amount shown in grey on the income dashboard — what the user must set
    /// aside from real income to eventually repay this BNPL charge.
    /// Defaults to the full Amount (lump-sum repayment model).
    /// </summary>
    [Column(TypeName = "REAL")]
    public decimal? BnplSetAsideAmount { get; set; }

    /// <summary>Optional: number of instalments the BNPL is spread over.</summary>
    public int? BnplInstallmentCount { get; set; }

    // ── Optional extra metadata ────────────────────────────────────────────────
    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(BnplSourceId))]
    public BnplSource? BnplSource { get; set; }

    public ICollection<ExpenseTag> ExpenseTags { get; set; } = [];
}