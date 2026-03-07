using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class BnplService : IBnplService
{
    private readonly IExpenseRepository _expenses;
    private readonly IBnplSourceRepository _sources;

    public BnplService(IBnplSourceRepository sources, IExpenseRepository expenses)
    {
        _sources = sources;
        _expenses = expenses;
    }

    public Task<IReadOnlyList<BnplSource>> GetActiveSourcesAsync()
    {
        return _sources.GetAllActiveAsync();
    }

    public async Task<BnplSource> AddSourceAsync(string name, BnplSourceType type,
        decimal? creditLimit = null, string? notes = null)
    {
        var source = new BnplSource
        {
            Name = name,
            Type = type,
            CreditLimit = creditLimit,
            CurrentBalance = 0,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
        await _sources.AddAsync(source);
        await _sources.SaveChangesAsync();
        return source;
    }

    public async Task UpdateSourceAsync(BnplSource source)
    {
        await _sources.UpdateAsync(source);
        await _sources.SaveChangesAsync();
    }

    public async Task DeactivateSourceAsync(int sourceId)
    {
        await _sources.DeactivateAsync(sourceId);
        await _sources.SaveChangesAsync();
    }

    /// <summary>
    ///     Repayment reduces the running balance on the source.
    ///     The delta is negative because we're paying it down.
    /// </summary>
    public async Task RecordRepaymentAsync(int sourceId, decimal amount)
    {
        if (amount <= 0) throw new ArgumentException("Repayment amount must be positive.", nameof(amount));
        await _sources.AdjustBalanceAsync(sourceId, -amount);
        await _sources.SaveChangesAsync();
    }

    public async Task<decimal> GetSetAsideForMonthAsync(int sourceId, int month, int year)
    {
        var expenses = await _expenses.GetBnplExpensesAsync(sourceId, month, year);
        return expenses.Sum(e => e.BnplSetAsideAmount ?? 0m);
    }
}