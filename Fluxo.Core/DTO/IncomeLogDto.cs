namespace Fluxo.Core.DTO;

public class IncomeLogDto
{
    public int Id { get; set; }
    public int AccountId { get; set; }
    public AccountDto Account { get; set; } = new();
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime AddedOn { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool IsForDeletion { get; set; }
    public bool IsPinned { get; set; }
    public bool IsDebt { get; set; }
}
