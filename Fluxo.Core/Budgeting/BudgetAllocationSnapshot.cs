namespace Fluxo.Core.Budgeting;

public sealed record BudgetAllocationSnapshot(
    BudgetAllocationPeriod CurrentPeriod,
    BudgetAllocationPeriod PreviousPeriod,
    decimal BudgetBase,
    decimal DailyAllowance,
    BudgetAllocationCategoryState Needs,
    BudgetAllocationCategoryState Wants,
    BudgetAllocationCategoryState Invest);
