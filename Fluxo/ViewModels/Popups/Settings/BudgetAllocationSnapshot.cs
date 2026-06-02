using Fluxo.Core.Enums;

namespace Fluxo.ViewModels.Popups.Settings;

public readonly record struct BudgetAllocationSnapshot(
    int Needs,
    int Wants,
    int Invest,
    decimal AllocationLimit,
    AllocationPeriod AllocationPeriod,
    RolloverPolicy RolloverPolicy,
    OverspendPolicy OverspendPolicy);
