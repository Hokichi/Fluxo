using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class ExpenseService : IExpenseService
{
    private readonly IBnplSourceRepository _bnplSources;
    private readonly IExpenseRepository _expenses;
    private readonly IAppSettingService _settings;
    private readonly ITagRepository _tags;

    public ExpenseService(
        IExpenseRepository expenses,
        IBnplSourceRepository bnplSources,
        ITagRepository tags,
        IAppSettingService settings)
    {
        _expenses = expenses;
        _bnplSources = bnplSources;
        _tags = tags;
        _settings = settings;
    }

    public Task<IReadOnlyList<Expense>> GetExpensesForMonthAsync(int month, int year)
    {
        return _expenses.GetByMonthAsync(month, year);
    }

    public Task<IReadOnlyList<Expense>> GetByTagAsync(int tagId, int? month = null, int? year = null)
    {
        return _expenses.GetByTagAsync(tagId, month, year);
    }

    public Task<IReadOnlyList<Expense>> GetBnplExpensesAsync(int? bnplSourceId = null, int? month = null,
        int? year = null)
    {
        return _expenses.GetBnplExpensesAsync(bnplSourceId, month, year);
    }

    public async Task<Expense> AddExpenseAsync(
        string description,
        decimal amount,
        ExpenseCategory category,
        DateTime? date = null,
        bool isBnpl = false,
        int? bnplSourceId = null,
        decimal? bnplSetAsideAmount = null,
        int? bnplInstallmentCount = null,
        IEnumerable<int>? tagIds = null,
        string? notes = null)
    {
        if (isBnpl && !bnplSourceId.HasValue)
            throw new ArgumentException("A BnplSourceId is required for BNPL expenses.", nameof(bnplSourceId));

        var entryDate = date ?? await _settings.GetDefaultEntryDateAsync();

        var expense = new Expense
        {
            Description = description,
            Amount = amount,
            Category = category,
            Date = entryDate,
            IsManualDate = date.HasValue,
            IsBnpl = isBnpl,
            BnplSourceId = bnplSourceId,
            // If no set-aside specified, user owes the full amount (lump-sum model).
            BnplSetAsideAmount = isBnpl ? bnplSetAsideAmount ?? amount : null,
            BnplInstallmentCount = bnplInstallmentCount,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        await _expenses.AddAsync(expense);

        // Attach tags
        var tagIdList = tagIds?.ToList() ?? [];
        foreach (var tagId in tagIdList)
            expense.ExpenseTags.Add(new ExpenseTag { TagId = tagId });

        // Charge the BNPL source balance
        if (isBnpl && bnplSourceId.HasValue)
            await _bnplSources.AdjustBalanceAsync(bnplSourceId.Value, amount);

        await _expenses.SaveChangesAsync();
        return expense;
    }

    public async Task<Expense> UpdateExpenseAsync(Expense expense, IEnumerable<int>? newTagIds = null)
    {
        if (newTagIds is not null)
        {
            expense.ExpenseTags.Clear();
            foreach (var tagId in newTagIds)
                expense.ExpenseTags.Add(new ExpenseTag { ExpenseId = expense.Id, TagId = tagId });
        }

        await _expenses.UpdateAsync(expense);
        await _expenses.SaveChangesAsync();
        return expense;
    }

    public async Task DeleteExpenseAsync(int expenseId)
    {
        var expense = await _expenses.GetByIdAsync(expenseId);
        if (expense is null) return;

        // Reverse the BNPL balance charge
        if (expense.IsBnpl && expense.BnplSourceId.HasValue)
            await _bnplSources.AdjustBalanceAsync(expense.BnplSourceId.Value, -expense.Amount);

        await _expenses.DeleteAsync(expenseId);
        await _expenses.SaveChangesAsync();
    }
}