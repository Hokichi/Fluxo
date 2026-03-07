using Fluxo.Core.DTOs;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

/// <summary>
///     Orchestrates all other services into a single DashboardSummary.
///     This is the only service the main ViewModel directly depends on.
/// </summary>
public sealed class DashboardService : IDashboardService
{
    private readonly IBnplService _bnpl;
    private readonly IBudgetService _budget;
    private readonly IFixedExpenseService _fixedExpenses;
    private readonly IIncomeService _income;
    private readonly ISavingsService _savings;
    private readonly IAppSettingService _settings;

    public DashboardService(
        IIncomeService income,
        IBudgetService budget,
        IFixedExpenseService fixedExpenses,
        ISavingsService savings,
        IBnplService bnpl,
        IAppSettingService settings)
    {
        _income = income;
        _budget = budget;
        _fixedExpenses = fixedExpenses;
        _savings = savings;
        _bnpl = bnpl;
        _settings = settings;
    }

    public async Task<DashboardSummary> GetSummaryAsync(int? month = null, int? year = null)
    {
        var today = DateTime.Today;
        var m = month ?? today.Month;
        var y = year ?? today.Year;

        // Fan out all independent queries in parallel for fast load.
        var totalIncomeTask = _income.GetTotalIncomeAsync(m, y);
        var sourceSummariesTask = _income.GetSourceSummariesAsync(m, y);
        var bnplSetAsideTask = _income.GetBnplSetAsideTotalAsync(m, y);
        var budgetTask = _budget.GetSummaryAsync(m, y);
        var pendingFixedTask = _fixedExpenses.GetPendingForMonthAsync(m, y);
        var estimatedFixedTask = _fixedExpenses.GetEstimatedMonthlyTotalAsync(m, y);
        var savingsAccountsTask = _savings.GetActiveAccountsAsync();
        var goalsTask = _savings.GetActiveGoalsAsync();
        var bnplSourcesTask = _bnpl.GetActiveSourcesAsync();
        var idleMoneyTask = _budget.GetIdleMoneyAsync(m, y);

        await Task.WhenAll(
            totalIncomeTask, sourceSummariesTask, bnplSetAsideTask, budgetTask,
            pendingFixedTask, estimatedFixedTask, savingsAccountsTask,
            goalsTask, bnplSourcesTask, idleMoneyTask);

        // Build savings account snapshots with 12-month projection
        var accountSnapshots = new List<SavingsAccountSnapshot>();
        foreach (var account in await savingsAccountsTask)
        {
            var projection = await _savings.ProjectAccountGrowthAsync(account.Id, 12);
            accountSnapshots.Add(new SavingsAccountSnapshot
            {
                AccountId = account.Id,
                Name = account.Name,
                CurrentBalance = account.CurrentBalance,
                AnnualInterestRate = account.AnnualInterestRate,
                ProjectedBalanceIn12Months = projection.FinalBalance
            });
        }

        // Build goal summaries
        var goalSummaries = new List<GoalProgressSummary>();
        foreach (var goal in await goalsTask)
            goalSummaries.Add(await _savings.GetGoalProgressAsync(goal.Id));

        // Build BNPL snapshots with per-source set-aside this month
        var bnplSnapshots = new List<BnplSourceSnapshot>();
        foreach (var source in await bnplSourcesTask)
        {
            var setAside = await _bnpl.GetSetAsideForMonthAsync(source.Id, m, y);
            bnplSnapshots.Add(new BnplSourceSnapshot
            {
                SourceId = source.Id,
                Name = source.Name,
                CurrentBalance = source.CurrentBalance,
                CreditLimit = source.CreditLimit,
                SetAsideThisMonth = setAside
            });
        }

        return new DashboardSummary
        {
            Month = m,
            Year = y,
            TotalIncome = await totalIncomeTask,
            IncomeSources = await sourceSummariesTask,
            BnplSetAsideTotal = await bnplSetAsideTask,
            Budget = await budgetTask,
            TotalMonthlyFixedExpenses = await estimatedFixedTask,
            PendingFixedExpenses = await pendingFixedTask,
            SavingsAccounts = accountSnapshots,
            SavingsGoals = goalSummaries,
            BnplSources = bnplSnapshots,
            IdleMoney = await idleMoneyTask
        };
    }

    public async Task<DashboardSummary> RefreshAsync(DashboardSummary current)
    {
        return await GetSummaryAsync(current.Month, current.Year);
    }
}