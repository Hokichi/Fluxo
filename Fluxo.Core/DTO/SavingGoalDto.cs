using Fluxo.Core.Enums;

namespace Fluxo.Core.DTO;

public class SavingGoalDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateTime? SavingEndDate { get; set; }
    public RecurringPeriod RecurringPeriod { get; set; }
    public DateTime CreatedOn { get; set; }
}
