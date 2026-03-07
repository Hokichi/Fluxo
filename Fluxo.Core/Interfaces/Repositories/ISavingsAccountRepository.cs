using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface ISavingsAccountRepository : IRepository<SavingsAccount>
{
    Task<IReadOnlyList<SavingsAccount>> GetAllActiveAsync();

    /// <summary>Persists a new CurrentBalance and stamps UpdatedAt.</summary>
    Task UpdateBalanceAsync(int id, decimal newBalance);

    Task DeactivateAsync(int id);
}