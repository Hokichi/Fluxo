using Fluxo.Core.DTO;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class AnalyticsService(IDataOperationRunner dataOperationRunner) : IAnalyticsService
{
    public async Task<AnalyticsDto> GetAnalyticsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (from > to)
            (from, to) = (to, from);

        return await dataOperationRunner.RunAsync("load analytics data", async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var expenseLogs = await unitOfWork.ExpenseLogs.GetAllAsync(ct);
            var incomeLogs = await unitOfWork.IncomeLogs.GetAllAsync(ct);
            var goals = await unitOfWork.SavingGoals.GetAllAsync(ct);

            var fromDate = from.ToDateTime(TimeOnly.MinValue);
            var toDate = to.ToDateTime(TimeOnly.MaxValue);

            var expenseInPeriod = expenseLogs
                .Where(log => !log.IsForDeletion &&
                              log.ParentLogId is null &&
                              log.DeductedOn >= fromDate &&
                              log.DeductedOn <= toDate)
                .ToList();

            var incomeInPeriod = incomeLogs
                .Where(log => log.AddedOn >= fromDate && log.AddedOn <= toDate)
                .ToList();

            var totalIncome = incomeInPeriod.Sum(log => log.Amount);
            var totalExpense = expenseInPeriod.Sum(log => log.Amount);

            var dateSequence = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
                .Select(offset => DateOnly.FromDayNumber(from.DayNumber + offset))
                .ToArray();

            var incomeByDay = incomeInPeriod
                .GroupBy(log => DateOnly.FromDateTime(log.AddedOn))
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

            var expenseByDay = expenseInPeriod
                .GroupBy(log => DateOnly.FromDateTime(log.DeductedOn))
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

            var timeSeries = dateSequence
                .Select(day => new AnalyticsTimeSeriesPoint(
                    day,
                    incomeByDay.TryGetValue(day, out var income) ? income : 0m,
                    expenseByDay.TryGetValue(day, out var expense) ? expense : 0m))
                .ToArray();

            var categoryTotals = expenseInPeriod
                .Where(log => log.Expense is not null)
                .GroupBy(log => log.Expense.ExpenseCategory)
                .ToDictionary(group => group.Key, group => group.Sum(item => item.Amount));

            var categoryRatio =
                new[]
                {
                    ExpenseCategory.Needs,
                    ExpenseCategory.Wants,
                    ExpenseCategory.Savings
                }
                .Select(category => new AnalyticsCategorySlice(
                    category,
                    categoryTotals.TryGetValue(category, out var total) ? total : 0m))
                .ToArray();

            var tagTotals = expenseInPeriod
                .Where(log => log.Expense?.Tag is not null)
                .GroupBy(log => new
                {
                    log.Expense.Tag.Id,
                    log.Expense.Tag.Name,
                    log.Expense.Tag.HexCode
                })
                .Select(group => new AnalyticsTagTotal(
                    group.Key.Name,
                    group.Key.HexCode,
                    group.Sum(item => item.Amount)))
                .OrderByDescending(item => item.Total)
                .ToArray();

            var goalsCreatedInPeriod = goals
                .Where(goal => goal.CreatedOn >= fromDate && goal.CreatedOn <= toDate)
                .OrderByDescending(goal => goal.CreatedOn)
                .ThenBy(goal => goal.Name)
                .Select(goal => new AnalyticsGoalItem(
                    goal.Id,
                    goal.Name,
                    goal.CurrentAmount,
                    goal.TargetAmount,
                    goal.CreatedOn,
                    goal.SavingEndDate))
                .ToArray();

            return new AnalyticsDto(
                TotalIncome: totalIncome,
                TotalExpense: totalExpense,
                TimeSeries: timeSeries,
                CategoryRatio: categoryRatio,
                TopSpendingTags: tagTotals,
                GoalsCreatedInPeriod: goalsCreatedInPeriod);
        }, cancellationToken);
    }
}
