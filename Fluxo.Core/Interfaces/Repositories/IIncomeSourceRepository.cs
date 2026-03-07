using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface IIncomeSourceRepository : IRepository<IncomeSource>
{
    /// <summary>Only returns sources where IsActive = true.</summary>
    Task<IReadOnlyList<IncomeSource>> GetAllActiveAsync();

    /// <summary>Soft-delete: sets IsActive = false, keeps historical entries intact.</summary>
    Task DeactivateAsync(int id);
}