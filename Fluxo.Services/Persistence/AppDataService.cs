using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class AppDataService(IUnitOfWork unitOfWork) : IAppDataService
{
    public Task<IReadOnlyList<Transaction>> GetTransactionsAsync(CancellationToken cancellationToken = default) =>
        unitOfWork.Transactions.GetAllAsync(cancellationToken);

    public Task<Transaction?> GetTransactionByIdAsync(int id, CancellationToken cancellationToken = default) =>
        unitOfWork.Transactions.GetByIdAsync(id, cancellationToken);

    public Task AddTransactionAsync(Transaction entity, CancellationToken cancellationToken = default) =>
        unitOfWork.Transactions.AddAsync(entity, cancellationToken);

    public void UpdateTransaction(Transaction entity) => unitOfWork.Transactions.Update(entity);
    public void RemoveTransaction(Transaction entity) => unitOfWork.Transactions.Remove(entity);

    public Task<IReadOnlyList<Expense>> GetExpensesAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.Expenses.GetAllAsync(cancellationToken);
    }

    public Task<Expense?> GetExpenseByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.Expenses.GetByIdAsync(id, cancellationToken);
    }

    public Task<Expense?> GetExpenseByExpenseIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.Expenses.GetByExpenseIdAsync(id, cancellationToken);
    }

    public Task AddExpenseAsync(Expense entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.Expenses.AddAsync(entity, cancellationToken);
    }

    public void UpdateExpense(Expense entity)
    {
        unitOfWork.Expenses.Update(entity);
    }

    public void RemoveExpense(Expense entity)
    {
        unitOfWork.Expenses.Remove(entity);
    }

    public Task<IReadOnlyList<ExpenseLog>> GetExpenseLogsAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExpenseLogs.GetAllAsync(cancellationToken);
    }

    public Task<ExpenseLog?> GetExpenseLogByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExpenseLogs.GetByIdAsync(id, cancellationToken);
    }

    public Task<ExpenseLog?> GetExpenseLogByLogIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExpenseLogs.GetByLogIdAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<ExpenseLog>> GetExpenseLogsByExpenseIdAsync(int expenseId,
        CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExpenseLogs.GetByExpenseIdAsync(expenseId, cancellationToken);
    }

    public Task<IReadOnlyList<ExpenseLog>> GetMarkedExpenseLogsAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExpenseLogs.GetMarkedForDeletionAsync(cancellationToken);
    }

    public Task AddExpenseLogAsync(ExpenseLog entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExpenseLogs.AddAsync(entity, cancellationToken);
    }

    public void UpdateExpenseLog(ExpenseLog entity)
    {
        unitOfWork.ExpenseLogs.Update(entity);
    }

    public void RemoveExpenseLog(ExpenseLog entity)
    {
        unitOfWork.ExpenseLogs.Remove(entity);
    }

    public Task<IReadOnlyList<IncomeLog>> GetIncomeLogsAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.IncomeLogs.GetAllAsync(cancellationToken);
    }

    public Task<IncomeLog?> GetIncomeLogByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.IncomeLogs.GetByIdAsync(id, cancellationToken);
    }

    public Task AddIncomeLogAsync(IncomeLog entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.IncomeLogs.AddAsync(entity, cancellationToken);
    }

    public void UpdateIncomeLog(IncomeLog entity)
    {
        unitOfWork.IncomeLogs.Update(entity);
    }

    public void RemoveIncomeLog(IncomeLog entity)
    {
        unitOfWork.IncomeLogs.Remove(entity);
    }

    public Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.Tags.GetAllAsync(cancellationToken);
    }

    public Task<Tag?> GetTagByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.Tags.GetByIdAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<(Tag Tag, int Count)>> GetTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default)
    {
        return unitOfWork.Tags.GetTagsByCountDescendingAsync(cancellationToken);
    }

    public Task AddTagAsync(Tag entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.Tags.AddAsync(entity, cancellationToken);
    }

    public void UpdateTag(Tag entity)
    {
        unitOfWork.Tags.Update(entity);
    }

    public void RemoveTag(Tag entity)
    {
        unitOfWork.Tags.Remove(entity);
    }

    public Task<IReadOnlyList<SavingGoal>> GetSavingGoalsAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.SavingGoals.GetAllAsync(cancellationToken);
    }

    public Task<SavingGoal?> GetSavingGoalByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.SavingGoals.GetByIdAsync(id, cancellationToken);
    }

    public Task AddSavingGoalAsync(SavingGoal entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.SavingGoals.AddAsync(entity, cancellationToken);
    }

    public void UpdateSavingGoal(SavingGoal entity)
    {
        unitOfWork.SavingGoals.Update(entity);
    }

    public void RemoveSavingGoal(SavingGoal entity)
    {
        unitOfWork.SavingGoals.Remove(entity);
    }

    public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.Accounts.GetAllAsync(cancellationToken);
    }

    public Task<Account?> GetAccountByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.Accounts.GetByIdAsync(id, cancellationToken);
    }

    public Task AddAccountAsync(Account entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.Accounts.AddAsync(entity, cancellationToken);
    }

    public void UpdateAccount(Account entity)
    {
        unitOfWork.Accounts.Update(entity);
    }

    public void RemoveAccount(Account entity)
    {
        unitOfWork.Accounts.Remove(entity);
    }

    public Task<IReadOnlyList<RecurringTransaction>> GetRecurringTransactionsAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.RecurringTransactions.GetAllAsync(cancellationToken);
    }

    public Task<RecurringTransaction?> GetRecurringTransactionByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.RecurringTransactions.GetByIdAsync(id, cancellationToken);
    }

    public Task AddRecurringTransactionAsync(RecurringTransaction entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.RecurringTransactions.AddAsync(entity, cancellationToken);
    }

    public void UpdateRecurringTransaction(RecurringTransaction entity)
    {
        unitOfWork.RecurringTransactions.Update(entity);
    }

    public void RemoveRecurringTransaction(RecurringTransaction entity)
    {
        unitOfWork.RecurringTransactions.Remove(entity);
    }

    public Task<IReadOnlyList<Notification>> GetNotificationsAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.Notifications.GetAllAsync(cancellationToken);
    }

    public Task<Notification?> GetNotificationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.Notifications.GetByIdAsync(id, cancellationToken);
    }

    public Task AddNotificationAsync(Notification entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.Notifications.AddAsync(entity, cancellationToken);
    }

    public void UpdateNotification(Notification entity)
    {
        unitOfWork.Notifications.Update(entity);
    }

    public void RemoveNotification(Notification entity)
    {
        unitOfWork.Notifications.Remove(entity);
    }

    public Task<IReadOnlyList<UserSettings>> GetUserSettingsAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.UserSettings.GetAllAsync(cancellationToken);
    }

    public Task<UserSettings?> GetUserSettingByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return unitOfWork.UserSettings.GetByNameAsync(name, cancellationToken);
    }

    public Task AddUserSettingAsync(UserSettings entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.UserSettings.AddAsync(entity, cancellationToken);
    }

    public void UpdateUserSetting(UserSettings entity)
    {
        unitOfWork.UserSettings.Update(entity);
    }

    public void RemoveUserSetting(UserSettings entity)
    {
        unitOfWork.UserSettings.Remove(entity);
    }

    public Task<BudgetAllocation> GetBudgetAllocationAsync(CancellationToken cancellationToken = default)
    {
        return EnsureBudgetAllocationAsync(cancellationToken);
    }

    public async Task<BudgetAllocation> EnsureBudgetAllocationAsync(CancellationToken cancellationToken = default)
    {
        var existing = await unitOfWork.BudgetAllocation.GetAsync(cancellationToken);
        if (existing is not null)
            return existing;

        var allocation = new BudgetAllocation();
        await unitOfWork.BudgetAllocation.AddAsync(allocation, cancellationToken);
        return allocation;
    }

    public void UpdateBudgetAllocation(BudgetAllocation entity)
    {
        unitOfWork.BudgetAllocation.Update(entity);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
