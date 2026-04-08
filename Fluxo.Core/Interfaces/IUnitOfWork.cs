using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.Core.Interfaces;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    IExpenseRepository Expenses { get; }
    IExpenseLogRepository ExpenseLogs { get; }
    IIncomeLogRepository IncomeLogs { get; }
    IExpenseTagRepository ExpenseTags { get; }
    ISavingGoalRepository SavingGoals { get; }
    ISpendingSourceRepository SpendingSources { get; }
    IUserSettingsRepository UserSettings { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}