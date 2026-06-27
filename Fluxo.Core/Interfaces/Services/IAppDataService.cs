using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Services;

public interface IAppDataService
{
    Task<IReadOnlyList<Transaction>> GetTransactionsAsync(CancellationToken cancellationToken = default);
    Task<Transaction?> GetTransactionByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddTransactionAsync(Transaction entity, CancellationToken cancellationToken = default);
    void UpdateTransaction(Transaction entity);
    void RemoveTransaction(Transaction entity);

    Task<IReadOnlyList<Expense>> GetExpensesAsync(CancellationToken cancellationToken = default);
    Task<Expense?> GetExpenseByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Expense?> GetExpenseByExpenseIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddExpenseAsync(Expense entity, CancellationToken cancellationToken = default);
    void UpdateExpense(Expense entity);
    void RemoveExpense(Expense entity);

    Task<IReadOnlyList<ExpenseLog>> GetExpenseLogsAsync(CancellationToken cancellationToken = default);
    Task<ExpenseLog?> GetExpenseLogByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ExpenseLog?> GetExpenseLogByLogIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetExpenseLogsByExpenseIdAsync(int expenseId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseLog>> GetMarkedExpenseLogsAsync(CancellationToken cancellationToken = default);
    Task AddExpenseLogAsync(ExpenseLog entity, CancellationToken cancellationToken = default);
    void UpdateExpenseLog(ExpenseLog entity);
    void RemoveExpenseLog(ExpenseLog entity);

    Task<IReadOnlyList<IncomeLog>> GetIncomeLogsAsync(CancellationToken cancellationToken = default);
    Task<IncomeLog?> GetIncomeLogByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddIncomeLogAsync(IncomeLog entity, CancellationToken cancellationToken = default);
    void UpdateIncomeLog(IncomeLog entity);
    void RemoveIncomeLog(IncomeLog entity);

    Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default);
    Task<Tag?> GetTagByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(Tag Tag, int Count)>> GetTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default);
    Task AddTagAsync(Tag entity, CancellationToken cancellationToken = default);
    void UpdateTag(Tag entity);
    void RemoveTag(Tag entity);

    Task<IReadOnlyList<SavingGoal>> GetSavingGoalsAsync(CancellationToken cancellationToken = default);
    Task<SavingGoal?> GetSavingGoalByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddSavingGoalAsync(SavingGoal entity, CancellationToken cancellationToken = default);
    void UpdateSavingGoal(SavingGoal entity);
    void RemoveSavingGoal(SavingGoal entity);

    Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default);
    Task<Account?> GetAccountByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAccountAsync(Account entity, CancellationToken cancellationToken = default);
    void UpdateAccount(Account entity);
    void RemoveAccount(Account entity);

    Task<IReadOnlyList<RecurringTransaction>> GetRecurringTransactionsAsync(CancellationToken cancellationToken = default);
    Task<RecurringTransaction?> GetRecurringTransactionByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddRecurringTransactionAsync(RecurringTransaction entity, CancellationToken cancellationToken = default);
    void UpdateRecurringTransaction(RecurringTransaction entity);
    void RemoveRecurringTransaction(RecurringTransaction entity);

    Task<IReadOnlyList<Notification>> GetNotificationsAsync(CancellationToken cancellationToken = default);
    Task<Notification?> GetNotificationByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddNotificationAsync(Notification entity, CancellationToken cancellationToken = default);
    void UpdateNotification(Notification entity);
    void RemoveNotification(Notification entity);

    Task<IReadOnlyList<UserSettings>> GetUserSettingsAsync(CancellationToken cancellationToken = default);
    Task<UserSettings?> GetUserSettingByNameAsync(string name, CancellationToken cancellationToken = default);
    Task AddUserSettingAsync(UserSettings entity, CancellationToken cancellationToken = default);
    void UpdateUserSetting(UserSettings entity);
    void RemoveUserSetting(UserSettings entity);

    Task<BudgetAllocation> GetBudgetAllocationAsync(CancellationToken cancellationToken = default);
    Task<BudgetAllocation> EnsureBudgetAllocationAsync(CancellationToken cancellationToken = default);
    void UpdateBudgetAllocation(BudgetAllocation entity);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
