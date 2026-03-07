using Fluxo.Core.Entities;
using Fluxo.Core.Enums;

namespace Fluxo.Core.Interfaces.Services;

public interface IBnplService
{
    Task<IReadOnlyList<BnplSource>> GetActiveSourcesAsync();

    Task<BnplSource> AddSourceAsync(string name, BnplSourceType type, decimal? creditLimit = null, string? notes = null);

    Task UpdateSourceAsync(BnplSource source);

    Task DeactivateSourceAsync(int sourceId);

    /// <summary>
    /// Records a repayment against a BNPL source, reducing CurrentBalance.
    /// </summary>
    Task RecordRepaymentAsync(int sourceId, decimal amount);

    /// <summary>
    /// Total BnplSetAsideAmount for expenses charged to a specific source in a given month.
    /// Used by DashboardService to build per-source set-aside snapshots.
    /// </summary>
    Task<decimal> GetSetAsideForMonthAsync(int sourceId, int month, int year);
}