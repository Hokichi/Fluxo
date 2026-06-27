using Fluxo.Core.Enums;

namespace Fluxo.Core.Filters;

public sealed class TransactionFilter
{
    public TransactionType? Type { get; init; }
    public int? AccountId { get; init; }
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public ExpenseCategory? ExpenseCategory { get; init; }
    public int? TagId { get; init; }
    public bool IncludeDeleted { get; init; }
}
