using Fluxo.Core.DTOs;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class BudgetService : IBudgetService
{
    private readonly IBudgetConfigRepository _configs;
    private readonly IExpenseRepository _expenses;
    private readonly IFixedExpenseHistoryRepository _fixedHistory;
    private readonly IIncomeEntryRepository _incomeEntries;

    public BudgetService(
        IBudgetConfigRepository configs,
        IIncomeEntryRepository incomeEntries,
        IExpenseRepository expenses,
        IFixedExpenseHistoryRepository fixedHistory)
    {
        _configs = configs;
        _incomeEntries = incomeEntries;
        _expenses = expenses;
        _fixedHistory = fixedHistory;
    }

    public async Task<MonthlyBudgetSummary> GetSummaryAsync(int month, int year)
    {
        var totalIncome = await _incomeEntries.GetTotalForMonthAsync(month, year);
        var bnplSetAside = await _expenses.GetBnplSetAsideTotalForMonthAsync(month, year);
        var config = await _configs.GetByMonthAsync(month, year);

        var needsPct = config?.NeedsPercentage ?? 50m;
        var wantsPct = config?.WantsPercentage ?? 30m;
        var savingsPct = config?.SavingsPercentage ?? 20m;

        var spentByCategory = await _expenses.GetTotalsByCategoryAsync(month, year);
        var fixedSpend = await _fixedHistory.GetTotalForMonthAsync(month, year);

        // Fixed expenses roll into Needs by default — already categorised there.
        decimal GetSpent(ExpenseCategory cat)
        {
            spentByCategory.TryGetValue(cat, out var variableSpend);
            return cat == ExpenseCategory.Needs
                ? variableSpend + fixedSpend
                : variableSpend;
        }

        BudgetBucket MakeBucket(ExpenseCategory cat, decimal pct)
        {
            return new()
            {
                Category = cat,
                Percentage = pct,
                Allocated = Math.Round(totalIncome * pct / 100, 2),
                Spent = GetSpent(cat)
            };
        }

        return new MonthlyBudgetSummary
        {
            Month = month,
            Year = year,
            TotalIncome = totalIncome,
            BnplSetAsideTotal = bnplSetAside,
            Needs = MakeBucket(ExpenseCategory.Needs, needsPct),
            Wants = MakeBucket(ExpenseCategory.Wants, wantsPct),
            Savings = MakeBucket(ExpenseCategory.Savings, savingsPct)
        };
    }

    public Task<BudgetConfig?> GetConfigAsync(int month, int year)
    {
        return _configs.GetByMonthAsync(month, year);
    }

    public async Task<BudgetConfig> UpsertConfigAsync(int month, int year,
        decimal needsPct, decimal wantsPct, decimal savingsPct)
    {
        var total = needsPct + wantsPct + savingsPct;
        if (Math.Abs(total - 100m) > 0.01m)
            throw new InvalidOperationException(
                $"Percentages must sum to 100. Got {total:F2}.");

        var config = new BudgetConfig
        {
            Month = month,
            Year = year,
            NeedsPercentage = needsPct,
            WantsPercentage = wantsPct,
            SavingsPercentage = savingsPct,
            UpdatedAt = DateTime.UtcNow
        };

        await _configs.UpsertAsync(config);
        await _configs.SaveChangesAsync();
        return config;
    }

    public async Task<decimal> GetSpentInCategoryAsync(ExpenseCategory category, int month, int year)
    {
        var totals = await _expenses.GetTotalsByCategoryAsync(month, year);
        totals.TryGetValue(category, out var variableSpend);

        if (category == ExpenseCategory.Needs)
            variableSpend += await _fixedHistory.GetTotalForMonthAsync(month, year);

        return variableSpend;
    }

    public async Task<decimal> GetIdleMoneyAsync(int month, int year)
    {
        var summary = await GetSummaryAsync(month, year);
        return summary.IdleMoney;
    }
}