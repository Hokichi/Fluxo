using Fluxo.Core.DTOs;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class IncomeService : IIncomeService
{
    private readonly IIncomeEntryRepository _entries;
    private readonly IExpenseRepository _expenses;
    private readonly IAppSettingService _settings;
    private readonly IIncomeSourceRepository _sources;

    public IncomeService(
        IIncomeSourceRepository sources,
        IIncomeEntryRepository entries,
        IExpenseRepository expenses,
        IAppSettingService settings)
    {
        _sources = sources;
        _entries = entries;
        _expenses = expenses;
        _settings = settings;
    }

    public Task<IReadOnlyList<IncomeSource>> GetActiveSourcesAsync()
    {
        return _sources.GetAllActiveAsync();
    }

    public async Task<IncomeSource> AddSourceAsync(string name, IncomeSourceType type, string? notes = null)
    {
        var source = new IncomeSource
        {
            Name = name,
            Type = type,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
        await _sources.AddAsync(source);
        await _sources.SaveChangesAsync();
        return source;
    }

    public async Task UpdateSourceAsync(IncomeSource source)
    {
        await _sources.UpdateAsync(source);
        await _sources.SaveChangesAsync();
    }

    public async Task DeactivateSourceAsync(int sourceId)
    {
        await _sources.DeactivateAsync(sourceId);
        await _sources.SaveChangesAsync();
    }

    public async Task<IncomeEntry> AddEntryAsync(int sourceId, decimal amount, DateTime? date = null,
        string? notes = null)
    {
        var entryDate = date ?? await _settings.GetDefaultEntryDateAsync();
        var isManual = date.HasValue;

        var entry = new IncomeEntry
        {
            IncomeSourceId = sourceId,
            Amount = amount,
            Date = entryDate,
            IsManualDate = isManual,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };

        await _entries.AddAsync(entry);
        await _entries.SaveChangesAsync();
        return entry;
    }

    public async Task UpdateEntryAsync(IncomeEntry entry)
    {
        await _entries.UpdateAsync(entry);
        await _entries.SaveChangesAsync();
    }

    public async Task DeleteEntryAsync(int entryId)
    {
        await _entries.DeleteAsync(entryId);
        await _entries.SaveChangesAsync();
    }

    public Task<IReadOnlyList<IncomeEntry>> GetEntriesForMonthAsync(int month, int year)
    {
        return _entries.GetByMonthAsync(month, year);
    }

    public Task<decimal> GetTotalIncomeAsync(int month, int year)
    {
        return _entries.GetTotalForMonthAsync(month, year);
    }

    public Task<decimal> GetBnplSetAsideTotalAsync(int month, int year)
    {
        return _expenses.GetBnplSetAsideTotalForMonthAsync(month, year);
    }

    public async Task<IReadOnlyList<IncomeSourceSummary>> GetSourceSummariesAsync(int month, int year)
    {
        var entries = await _entries.GetByMonthAsync(month, year);
        return entries
            .GroupBy(e => new { e.IncomeSourceId, e.IncomeSource.Name })
            .Select(g => new IncomeSourceSummary
            {
                SourceId = g.Key.IncomeSourceId,
                Name = g.Key.Name,
                TotalThisMonth = g.Sum(e => e.Amount)
            })
            .OrderByDescending(s => s.TotalThisMonth)
            .ToList();
    }
}