using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.Data.Repositories;

public sealed class SavingsGoalRepository : BaseRepository<SavingsGoal>, ISavingsGoalRepository
{
    public SavingsGoalRepository(AppDbContext db) : base(db) { }

    public async Task<IReadOnlyList<SavingsGoal>> GetAllActiveAsync()
        => await Db.SavingsGoals
            .Where(g => g.IsActive && !g.IsCompleted)
            .OrderBy(g => g.EstimatedCompletionDate)
            .ToListAsync();

    public async Task<IReadOnlyList<SavingsGoal>> GetCompletedAsync()
        => await Db.SavingsGoals
            .Where(g => g.IsCompleted)
            .OrderByDescending(g => g.CompletedDate)
            .ToListAsync();

    public async Task UpdateProgressAsync(int id, decimal newCurrentAmount)
    {
        var goal = await Db.SavingsGoals.FindAsync(id)
                   ?? throw new InvalidOperationException($"SavingsGoal {id} not found.");
        goal.CurrentAmount = newCurrentAmount;
        goal.UpdatedAt = DateTime.UtcNow;
        Db.SavingsGoals.Update(goal);
    }

    public async Task MarkCompletedAsync(int id, DateTime completedDate)
    {
        var goal = await Db.SavingsGoals.FindAsync(id);
        if (goal is null) return;
        goal.IsCompleted = true;
        goal.CompletedDate = completedDate;
        goal.CurrentAmount = goal.TargetAmount;
        goal.UpdatedAt = DateTime.UtcNow;
        Db.SavingsGoals.Update(goal);
    }

    public async Task DeactivateAsync(int id)
    {
        var goal = await Db.SavingsGoals.FindAsync(id);
        if (goal is null) return;
        goal.IsActive = false;
        Db.SavingsGoals.Update(goal);
    }
}