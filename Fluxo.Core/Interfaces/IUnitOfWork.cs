using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.Core.Interfaces;

public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    IExpenseRepository Expenses { get; }
    IExpenseLogRepository ExpenseLogs { get; }
    IIncomeLogRepository IncomeLogs { get; }
    ITagRepository Tags { get; }
    ISavingGoalRepository SavingGoals { get; }
    IAccountRepository Accounts { get; }
    IRecurringTransactionRepository RecurringTransactions { get; }
    INotificationRepository Notifications { get; }
    IUserSettingsRepository UserSettings { get; }
    IBudgetAllocationRepository BudgetAllocation { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
