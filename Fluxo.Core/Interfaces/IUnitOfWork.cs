using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.Core.Interfaces;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    ITransactionRepository Transactions { get; }
    ITagRepository Tags { get; }
    ISavingGoalRepository SavingGoals { get; }
    IAccountRepository Accounts { get; }
    IRecurringTransactionRepository RecurringTransactions { get; }
    IUserSettingsRepository UserSettings { get; }
    IBudgetAllocationRepository BudgetAllocation { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
