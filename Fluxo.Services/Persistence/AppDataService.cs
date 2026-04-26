using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class AppDataService(IUnitOfWork unitOfWork) : IAppDataService
{
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

    public Task<IReadOnlyList<ExpenseTag>> GetExpenseTagsAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExpenseTags.GetAllAsync(cancellationToken);
    }

    public Task<ExpenseTag?> GetExpenseTagByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExpenseTags.GetByIdAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<(ExpenseTag Tag, int Count)>> GetExpenseTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExpenseTags.GetTagsByCountDescendingAsync(cancellationToken);
    }

    public Task AddExpenseTagAsync(ExpenseTag entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.ExpenseTags.AddAsync(entity, cancellationToken);
    }

    public void UpdateExpenseTag(ExpenseTag entity)
    {
        unitOfWork.ExpenseTags.Update(entity);
    }

    public void RemoveExpenseTag(ExpenseTag entity)
    {
        unitOfWork.ExpenseTags.Remove(entity);
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

    public Task<IReadOnlyList<SpendingSource>> GetSpendingSourcesAsync(CancellationToken cancellationToken = default)
    {
        return unitOfWork.SpendingSources.GetAllAsync(cancellationToken);
    }

    public Task<SpendingSource?> GetSpendingSourceByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return unitOfWork.SpendingSources.GetByIdAsync(id, cancellationToken);
    }

    public Task AddSpendingSourceAsync(SpendingSource entity, CancellationToken cancellationToken = default)
    {
        return unitOfWork.SpendingSources.AddAsync(entity, cancellationToken);
    }

    public void UpdateSpendingSource(SpendingSource entity)
    {
        unitOfWork.SpendingSources.Update(entity);
    }

    public void RemoveSpendingSource(SpendingSource entity)
    {
        unitOfWork.SpendingSources.Remove(entity);
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
