using Fluxo.Core.Enums;

namespace Fluxo.Core.Budgeting;

public readonly record struct BudgetAllocationCategoryState(
    BudgetAllocationSegment Segment,
    decimal BaseAllocation,
    decimal Rollover,
    decimal Debt,
    decimal Spent,
    decimal Available,
    decimal Remaining,
    int Percentage);
