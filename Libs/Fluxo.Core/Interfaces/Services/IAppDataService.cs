using Fluxo.Core.Entities;

namespace Fluxo.Core.Interfaces.Services;

public interface IAppDataService
{
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

    Task<IReadOnlyList<ExpenseTag>> GetExpenseTagsAsync(CancellationToken cancellationToken = default);
    Task<ExpenseTag?> GetExpenseTagByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(ExpenseTag Tag, int Count)>> GetExpenseTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default);
    Task AddExpenseTagAsync(ExpenseTag entity, CancellationToken cancellationToken = default);
    void UpdateExpenseTag(ExpenseTag entity);
    void RemoveExpenseTag(ExpenseTag entity);

    Task<IReadOnlyList<SavingGoal>> GetSavingGoalsAsync(CancellationToken cancellationToken = default);
    Task<SavingGoal?> GetSavingGoalByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddSavingGoalAsync(SavingGoal entity, CancellationToken cancellationToken = default);
    void UpdateSavingGoal(SavingGoal entity);
    void RemoveSavingGoal(SavingGoal entity);

    Task<IReadOnlyList<SpendingSource>> GetSpendingSourcesAsync(CancellationToken cancellationToken = default);
    Task<SpendingSource?> GetSpendingSourceByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddSpendingSourceAsync(SpendingSource entity, CancellationToken cancellationToken = default);
    void UpdateSpendingSource(SpendingSource entity);
    void RemoveSpendingSource(SpendingSource entity);

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

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
