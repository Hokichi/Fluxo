using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Filters;

public class ExpenseFilter
{
    public string Name { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public ExpenseCategory? Category { get; set; }
    public ExpenseTag? Tag { get; set; }
    public int? TagId { get; set; }
    public bool ShouldFilterDeletion { get; set; }
}
