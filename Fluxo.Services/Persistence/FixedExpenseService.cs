using Fluxo.Core.DTOs;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class FixedExpenseService : IFixedExpenseService
{
    private readonly IFixedExpenseRepository _fixedExpenses;
    private readonly IFixedExpenseHistoryRepository _history;
    private readonly IAppSettingService _settings;

    public FixedExpenseService(
        IFixedExpenseRepository fixedExpenses,
        IFixedExpenseHistoryRepository history,
        IAppSettingService settings)
    {
        _fixedExpenses = fixedExpenses;
        _history = history;
        _settings = settings;
    }

    public Task<IReadOnlyList<FixedExpense>> GetAllActiveAsync()
    {
        return _fixedExpenses.GetAllActiveAsync();
    }

    public async Task<IReadOnlyList<FixedExpenseDueSummary>> GetDueSoonAsync(int daysAhead)
    {
        var upcoming = await _fixedExpenses.GetDueSoonAsync(daysAhead);
        return await ToSummariesAsync(upcoming);
    }

    public async Task<IReadOnlyList<FixedExpenseDueSummary>> GetPendingForMonthAsync(int month, int year)
    {
        var unpaid = await _fixedExpenses.GetUnpaidForMonthAsync(month, year);
        return await ToSummariesAsync(unpaid, month, year);
    }

    private async Task<IReadOnlyList<FixedExpenseDueSummary>> ToSummariesAsync(
        IReadOnlyList<FixedExpense> expenses, int? month = null, int? year = null)
    {
        var today = DateTime.Today;
        var results = new List<FixedExpenseDueSummary>();

        foreach (var fe in expenses)
        {
            decimal? avg = fe.AmountMode == FixedExpenseAmountMode.Variable
                ? await _history.GetAverageAmountAsync(fe.Id)
                : null;

            var m = month ?? today.Month;
            var y = year ?? today.Year;
            var clampedDay = Math.Min(fe.DueDay, DateTime.DaysInMonth(y, m));
            var dueDate = new DateTime(y, m, clampedDay);

            results.Add(new FixedExpenseDueSummary
            {
                FixedExpenseId = fe.Id,
                Name = fe.Name,
                Category = fe.Category,
                Amount = fe.AmountMode == FixedExpenseAmountMode.Fixed ? fe.Amount : null,
                AverageHistoricalAmount = avg,
                DueDay = fe.DueDay,
                DueDate = dueDate,
                RequiresAmountInput = fe.AmountMode == FixedExpenseAmountMode.Variable,
                Tags = fe.FixedExpenseTags.Select(ft => ft.Tag.Name).ToList()
            });
        }

        return results;
    }

    public async Task<FixedExpense> AddFixedExpenseAsync(
        string name,
        FixedExpenseAmountMode amountMode,
        decimal? amount,
        int dueDay,
        ExpenseCategory category,
        bool notificationEnabled = true,
        IEnumerable<int>? tagIds = null,
        string? notes = null)
    {
        if (amountMode == FixedExpenseAmountMode.Fixed && !amount.HasValue)
            throw new ArgumentException("Amount is required for fixed-mode expenses.", nameof(amount));
        if (dueDay is < 1 or > 28)
            throw new ArgumentOutOfRangeException(nameof(dueDay), "DueDay must be between 1 and 28.");

        var fe = new FixedExpense
        {
            Name = name,
            AmountMode = amountMode,
            Amount = amount,
            DueDay = dueDay,
            Category = category,
            NotificationEnabled = notificationEnabled,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var tagId in tagIds ?? [])
            fe.FixedExpenseTags.Add(new FixedExpenseTag { TagId = tagId });

        await _fixedExpenses.AddAsync(fe);
        await _fixedExpenses.SaveChangesAsync();
        return fe;
    }

    public async Task<FixedExpense> UpdateFixedExpenseAsync(FixedExpense expense, IEnumerable<int>? newTagIds = null)
    {
        if (newTagIds is not null)
        {
            expense.FixedExpenseTags.Clear();
            foreach (var tagId in newTagIds)
                expense.FixedExpenseTags.Add(new FixedExpenseTag { FixedExpenseId = expense.Id, TagId = tagId });
        }

        await _fixedExpenses.UpdateAsync(expense);
        await _fixedExpenses.SaveChangesAsync();
        return expense;
    }

    public async Task ConfirmPaymentAsync(int fixedExpenseId, decimal? amount = null, DateTime? paidDate = null)
    {
        var fe = await _fixedExpenses.GetByIdAsync(fixedExpenseId)
                 ?? throw new InvalidOperationException($"FixedExpense {fixedExpenseId} not found.");

        var resolvedAmount = fe.AmountMode switch
        {
            FixedExpenseAmountMode.Fixed => amount ?? fe.Amount
                ?? throw new InvalidOperationException("Fixed expense has no stored amount."),
            FixedExpenseAmountMode.Variable => amount
                                               ?? throw new ArgumentNullException(nameof(amount),
                                                   "Amount must be provided for variable fixed expenses."),
            _ => throw new InvalidOperationException("Unknown AmountMode.")
        };

        var paymentDate = paidDate ?? DateTime.Today;

        // Write history row (powers Trends and variable-expense average)
        var historyRow = new FixedExpenseHistory
        {
            FixedExpenseId = fixedExpenseId,
            Amount = resolvedAmount,
            PaidDate = paymentDate,
            CreatedAt = DateTime.UtcNow
        };
        await _history.AddAsync(historyRow);

        // Update the parent record
        await _fixedExpenses.MarkAsPaidAsync(fixedExpenseId, paymentDate);
        await _fixedExpenses.SaveChangesAsync();
    }

    public async Task DeactivateAsync(int fixedExpenseId)
    {
        await _fixedExpenses.DeactivateAsync(fixedExpenseId);
        await _fixedExpenses.SaveChangesAsync();
    }

    public async Task<decimal> GetEstimatedMonthlyTotalAsync(int month, int year)
    {
        var active = await _fixedExpenses.GetAllActiveAsync();
        var total = 0m;

        foreach (var fe in active)
            if (fe.AmountMode == FixedExpenseAmountMode.Fixed)
            {
                total += fe.Amount ?? 0m;
            }
            else
            {
                // Use historical average as the estimate for variable expenses
                var avg = await _history.GetAverageAmountAsync(fe.Id);
                total += avg ?? 0m;
            }

        return total;
    }
}