using Fluxo.Core.DTOs;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class TrendService : ITrendService
{
    private readonly IExpenseRepository _expenses;
    private readonly IFixedExpenseHistoryRepository _fixedHistory;
    private readonly IIncomeEntryRepository _incomeEntries;
    private readonly ISavingsAccountRepository _savingsAccounts;
    private readonly ITagRepository _tags;

    public TrendService(
        IIncomeEntryRepository incomeEntries,
        IExpenseRepository expenses,
        IFixedExpenseHistoryRepository fixedHistory,
        ISavingsAccountRepository savingsAccounts,
        ITagRepository tags)
    {
        _incomeEntries = incomeEntries;
        _expenses = expenses;
        _fixedHistory = fixedHistory;
        _savingsAccounts = savingsAccounts;
        _tags = tags;
    }

    public Task<TrendReport> GetMonthlyReportAsync(int month, int year)
    {
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return GetDateRangeReportAsync(from, to);
    }

    public Task<TrendReport> GetRollingReportAsync(int pastMonths = 6)
    {
        var to = DateTime.Today;
        var from = new DateTime(to.Year, to.Month, 1).AddMonths(-pastMonths + 1);
        return GetDateRangeReportAsync(from, to);
    }

    public async Task<TrendReport> GetDateRangeReportAsync(DateTime from, DateTime to)
    {
        var incomeEntries = await _incomeEntries.GetByDateRangeAsync(from, to);
        var variableExpenses = await _expenses.GetByDateRangeAsync(from, to);
        var fixedHistory = await _fixedHistory.GetByDateRangeAsync(from, to);
        var allTags = await _tags.GetAllAsync();

        var totalIncome = incomeEntries.Sum(e => e.Amount);
        var totalVariable = variableExpenses.Sum(e => e.Amount);
        var totalFixed = fixedHistory.Sum(h => h.Amount);
        var totalBnplCharged = variableExpenses.Where(e => e.IsBnpl).Sum(e => e.Amount);
        var totalBnplSetAside = variableExpenses.Where(e => e.IsBnpl).Sum(e => e.BnplSetAsideAmount ?? 0);

        // ── Bucket trends ─────────────────────────────────────────────────────
        var monthSpan = Math.Max(1, GetMonthCount(from, to));
        var spentByCategory = variableExpenses
            .GroupBy(e => e.Category)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        // Fixed expenses are treated as Needs
        spentByCategory.TryGetValue(ExpenseCategory.Needs, out var needsVar);
        spentByCategory[ExpenseCategory.Needs] = needsVar + totalFixed;

        var bucketTrends = Enum.GetValues<ExpenseCategory>().Select(cat =>
        {
            spentByCategory.TryGetValue(cat, out var spent);
            return new BucketTrend
            {
                Category = cat,
                TotalSpent = spent,
                AverageMonthly = Math.Round(spent / monthSpan, 2),
                PercentOfIncome = totalIncome == 0 ? 0 : Math.Round(spent / totalIncome * 100, 1)
            };
        }).ToList();

        // ── Tag spend breakdown ───────────────────────────────────────────────
        var totalExpenses = totalVariable + totalFixed;
        var tagSummaries = variableExpenses
            .SelectMany(e => e.ExpenseTags, (e, et) => new { et.TagId, e.Amount })
            .GroupBy(x => x.TagId)
            .Select(g =>
            {
                var tag = allTags.FirstOrDefault(t => t.Id == g.Key);
                return new TagSpendSummary
                {
                    TagId = g.Key,
                    TagName = tag?.Name ?? "Unknown",
                    TagColor = tag?.Color ?? "#808080",
                    TotalSpent = g.Sum(x => x.Amount),
                    TransactionCount = g.Count(),
                    PercentOfTotalExpenses = totalExpenses == 0
                        ? 0
                        : Math.Round(g.Sum(x => x.Amount) / totalExpenses * 100, 1)
                };
            })
            .OrderByDescending(t => t.TotalSpent)
            .ToList();

        // ── Month-over-month breakdown ────────────────────────────────────────
        var monthly = BuildMonthlyBreakdown(from, to, incomeEntries, variableExpenses, fixedHistory);

        // ── Idle money ────────────────────────────────────────────────────────
        var savingsAccounts = await _savingsAccounts.GetAllActiveAsync();
        var totalSaved = savingsAccounts.Sum(a => a.CurrentBalance - a.InitialBalance);
        var idle = totalIncome - totalVariable - totalFixed - Math.Max(0, totalSaved);

        return new TrendReport
        {
            From = from,
            To = to,
            TotalIncome = totalIncome,
            TotalExpenses = totalVariable,
            TotalFixedExpenses = totalFixed,
            TotalSaved = Math.Max(0, totalSaved),
            BucketTrends = bucketTrends,
            TopTagSpends = tagSummaries,
            AverageMonthlyIncome = Math.Round(totalIncome / monthSpan, 2),
            AverageMonthlyExpenses = Math.Round((totalVariable + totalFixed) / monthSpan, 2),
            IdleMoney = idle,
            TotalBnplCharged = totalBnplCharged,
            TotalBnplSetAside = totalBnplSetAside,
            MonthlyBreakdown = monthly
        };
    }

    public async Task<decimal> GetIdleMoneyAsync(int month, int year)
    {
        var income = await _incomeEntries.GetTotalForMonthAsync(month, year);
        var spentByCategory = await _expenses.GetTotalsByCategoryAsync(month, year);
        var fixed_ = await _fixedHistory.GetTotalForMonthAsync(month, year);
        return income - spentByCategory.Values.Sum() - fixed_;
    }

    private static int GetMonthCount(DateTime from, DateTime to)
    {
        return (to.Year - from.Year) * 12 + to.Month - from.Month + 1;
    }

    private static IReadOnlyList<MonthlySpendPoint> BuildMonthlyBreakdown(
        DateTime from, DateTime to,
        IEnumerable<Core.Entities.IncomeEntry> incomeEntries,
        IEnumerable<Core.Entities.Expense> expenses,
        IEnumerable<Core.Entities.FixedExpenseHistory> fixedHistory)
    {
        var points = new List<MonthlySpendPoint>();
        var current = new DateTime(from.Year, from.Month, 1);
        var end = new DateTime(to.Year, to.Month, 1);

        while (current <= end)
        {
            var m = current.Month;
            var y = current.Year;

            points.Add(new MonthlySpendPoint
            {
                Month = m,
                Year = y,
                Income = incomeEntries.Where(e => e.Date.Month == m && e.Date.Year == y).Sum(e => e.Amount),
                Expenses = expenses.Where(e => e.Date.Month == m && e.Date.Year == y).Sum(e => e.Amount),
                FixedExpenses = fixedHistory.Where(h => h.PaidDate.Month == m && h.PaidDate.Year == y)
                    .Sum(h => h.Amount),
                Savings = 0 // populated separately if needed
            });

            current = current.AddMonths(1);
        }

        return points;
    }
}