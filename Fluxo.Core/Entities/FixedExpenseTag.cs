using System.ComponentModel.DataAnnotations.Schema;

namespace Fluxo.Core.Entities;

[Table("FixedExpenseTags")]
public class FixedExpenseTag
{
    public int FixedExpenseId { get; set; }
    public int TagId { get; set; }

    [ForeignKey(nameof(FixedExpenseId))]
    public FixedExpense FixedExpense { get; set; } = null!;

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;
}