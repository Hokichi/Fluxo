using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Fluxo.Core.Entities;

[Table("FixedExpenseHistory")]
public class FixedExpenseHistory
{
    [Key]
    public int Id { get; set; }

    public int FixedExpenseId { get; set; }

    [Column(TypeName = "REAL")]
    public decimal Amount { get; set; }

    /// <summary>Date the payment was made / confirmed.</summary>
    public DateTime PaidDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(FixedExpenseId))]
    public FixedExpense FixedExpense { get; set; } = null!;
}