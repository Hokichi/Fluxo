using System.ComponentModel.DataAnnotations.Schema;

namespace Fluxo.Core.Entities;

[Table("ExpenseTags")]
public class ExpenseTag
{
    public int ExpenseId { get; set; }
    public int TagId { get; set; }

    [ForeignKey(nameof(ExpenseId))]
    public Expense Expense { get; set; } = null!;

    [ForeignKey(nameof(TagId))]
    public Tag Tag { get; set; } = null!;
}