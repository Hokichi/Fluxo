using Fluxo.Core.DTOs;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Services;

public interface IIncomeService
{
    // ── Sources ───────────────────────────────────────────────────────────────
    Task<IReadOnlyList<IncomeSource>> GetActiveSourcesAsync();

    Task<IncomeSource> AddSourceAsync(string name, IncomeSourceType type, string? notes = null);

    Task UpdateSourceAsync(IncomeSource source);

    /// <summary>Soft-deletes the source. Existing entries are preserved.</summary>
    Task DeactivateSourceAsync(int sourceId);

    // ── Entries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Records a new income deposit.
    /// When <paramref name="date"/> is null the service uses the configured
    /// DefaultEntryDay (1st of current month by default).
    /// </summary>
    Task<IncomeEntry> AddEntryAsync(int sourceId, decimal amount, DateTime? date = null, string? notes = null);

    Task UpdateEntryAsync(IncomeEntry entry);

    Task DeleteEntryAsync(int entryId);

    Task<IReadOnlyList<IncomeEntry>> GetEntriesForMonthAsync(int month, int year);

    /// <summary>
    /// Total gross income for the month — the base for 50/30/20 and dashboard calculations.
    /// </summary>
    Task<decimal> GetTotalIncomeAsync(int month, int year);

    /// <summary>
    /// Total BNPL set-aside for the month — the grey "spoken for" overlay.
    /// </summary>
    Task<decimal> GetBnplSetAsideTotalAsync(int month, int year);

    /// <summary>Per-source income breakdown for the dashboard income panel.</summary>
    Task<IReadOnlyList<IncomeSourceSummary>> GetSourceSummariesAsync(int month, int year);
}