using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;

namespace Fluxo.Data;

public sealed class UnitOfWork(
    FluxoDbContext dbContext,
    IExpenseRepository expenses,
    IExpenseLogRepository expenseLogs,
    IIncomeLogRepository incomeLogs,
    IExpenseTagRepository expenseTags,
    ISavingGoalRepository savingGoals,
    ISpendingSourceRepository spendingSources,
    IRecurringTransactionRepository recurringTransactions,
    INotificationRepository notifications,
    IUserSettingsRepository userSettings) : IUnitOfWork
{
    private readonly FluxoDbContext _dbContext = dbContext;

    public IExpenseRepository Expenses { get; } = expenses;
    public IExpenseLogRepository ExpenseLogs { get; } = expenseLogs;
    public IIncomeLogRepository IncomeLogs { get; } = incomeLogs;
    public IExpenseTagRepository ExpenseTags { get; } = expenseTags;
    public ISavingGoalRepository SavingGoals { get; } = savingGoals;
    public ISpendingSourceRepository SpendingSources { get; } = spendingSources;
    public IRecurringTransactionRepository RecurringTransactions { get; } = recurringTransactions;
    public INotificationRepository Notifications { get; } = notifications;
    public IUserSettingsRepository UserSettings { get; } = userSettings;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _dbContext.DisposeAsync();
    }
}
