using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IBnplSourceRepository : IRepository<BnplSource>
{
    Task<IReadOnlyList<BnplSource>> GetAllActiveAsync();

    /// <summary>
    /// Adjusts CurrentBalance by +delta (charge) or -delta (repayment).
    /// Keeps the balance in sync whenever an expense is added/deleted.
    /// </summary>
    Task AdjustBalanceAsync(int id, decimal delta);

    Task DeactivateAsync(int id);
}