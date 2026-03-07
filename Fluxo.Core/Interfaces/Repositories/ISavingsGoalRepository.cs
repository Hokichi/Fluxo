using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Repositories;

public interface ISavingsGoalRepository : IRepository<SavingsGoal>
{
    Task<IReadOnlyList<SavingsGoal>> GetAllActiveAsync();

    Task<IReadOnlyList<SavingsGoal>> GetCompletedAsync();

    /// <summary>Updates CurrentAmount, recalculates EstimatedCompletionDate, stamps UpdatedAt.</summary>
    Task UpdateProgressAsync(int id, decimal newCurrentAmount);

    Task MarkCompletedAsync(int id, DateTime completedDate);

    Task DeactivateAsync(int id);
}