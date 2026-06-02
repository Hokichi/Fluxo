namespace Fluxo.Core.Budgeting;

public readonly record struct BudgetAllocationPeriod(DateTime Start, DateTime End)
{
    public int DayCount => (End.Date - Start.Date).Days + 1;
}
