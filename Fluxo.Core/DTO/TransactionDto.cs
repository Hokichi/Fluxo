using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public sealed class TransactionDto
{
    public int Id { get; set; }
    public TransactionType Type { get; set; }
    public int AccountId { get; set; }
    public AccountDto Account { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OccurredOn { get; set; }
    public string Notes { get; set; } = string.Empty;
    public ExpenseCategory? ExpenseCategory { get; set; }
    public int? TagId { get; set; }
    public TagDto? Tag { get; set; }
    public int? ParentTransactionId { get; set; }
    public bool IsPinned { get; set; }
    public bool IsForDeletion { get; set; }
    public bool IsIoU { get; set; }
    public bool IsExcludedFromBudget { get; set; }
}
