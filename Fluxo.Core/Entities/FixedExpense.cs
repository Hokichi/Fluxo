using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

[Table("FixedExpenses")]
public class FixedExpense
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public FixedExpenseAmountMode AmountMode { get; set; } = FixedExpenseAmountMode.Fixed;

    /// <summary>
    /// Null when AmountMode = Variable (user enters it each month).
    /// </summary>
    [Column(TypeName = "REAL")]
    public decimal? Amount { get; set; }

    /// <summary>Day of month the bill is due. Range 1-28.</summary>
    public int DueDay { get; set; } = 1;

    /// <summary>Which 50/30/20 bucket this fixed expense falls into.</summary>
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Needs;

    public bool IsActive { get; set; } = true;

    public bool NotificationEnabled { get; set; } = true;

    /// <summary>Date the most recent cycle was marked as paid.</summary>
    public DateTime? LastPaidDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<FixedExpenseHistory> History { get; set; } = [];

    public ICollection<FixedExpenseTag> FixedExpenseTags { get; set; } = [];
}