using Fluxo.Core.Enums;

namespace Fluxo.Core.Entities;

public sealed class BudgetAllocation
{
    public int Id { get; set; }
    public int NeedsThreshold { get; set; } = 50;
    public int WantsThreshold { get; set; } = 30;
    public int InvestThreshold { get; set; } = 20;
    public AllocationPeriod AllocationPeriod { get; set; } = AllocationPeriod.Monthly;
    public decimal AllocationLimit { get; set; }
    public decimal NeedsDebt { get; set; }
    public decimal WantsDebt { get; set; }
    public decimal InvestDebt { get; set; }
    public RolloverPolicy RolloverPolicy { get; set; } = RolloverPolicy.None;
    public OverspendPolicy OverspendPolicy { get; set; } = OverspendPolicy.Ignore;
}
