using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public class ExpenseDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public int TagId { get; set; }
    public AccountDto Account { get; set; } = new();
    public TagDto Tag { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; }
    public bool IsIoU { get; set; }
}
