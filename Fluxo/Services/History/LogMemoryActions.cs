using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;

namespace Fluxo.Services.History;

public interface ILogMemoryAction
{
    string Description { get; }

    Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);

    Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default);
}

public sealed record SpendingSourceMemorySnapshot(
    int SpendingSourceId,
    string Name,
    SpendingSourceType SpendingSourceType,
    decimal AccountLimit,
    decimal SpentAmount,
    decimal Balance,
    int? MonthlyDueDate,
    int? DeductSource,
    decimal? InterestRate,
    bool ShowOnUI,
    bool IsEnabled)
{
    public static SpendingSourceMemorySnapshot Create(SpendingSource spendingSource)
    {
        ArgumentNullException.ThrowIfNull(spendingSource);

        return new SpendingSourceMemorySnapshot(
            spendingSource.Id,
            spendingSource.Name,
            spendingSource.SpendingSourceType,
            spendingSource.AccountLimit,
            spendingSource.SpentAmount,
            spendingSource.Balance,
            spendingSource.MonthlyDueDate,
            spendingSource.DeductSource,
            spendingSource.InterestRate,
            spendingSource.ShowOnUI,
            spendingSource.IsEnabled);
    }
}

public sealed record ExpenseMemorySnapshot(
    int ExpenseId,
    int SpendingSourceId,
    int TagId,
    string Name,
    decimal Amount,
    ExpenseKind ExpenseKind,
    ExpenseCategory ExpenseCategory,
    int? RecurringDate,
    bool IsActive)
{
    public static ExpenseMemorySnapshot Create(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        return new ExpenseMemorySnapshot(
            expense.Id,
            expense.SpendingSourceId,
            expense.ExpenseTagId,
            expense.Name,
            expense.Amount,
            expense.ExpenseKind,
            expense.ExpenseCategory,
            expense.RecurringDate,
            expense.IsActive);
    }
}

public sealed record ExpenseTagMemorySnapshot(
    int ExpenseTagId,
    string Name,
    string HexCode)
{
    public static ExpenseTagMemorySnapshot Create(ExpenseTag expenseTag)
    {
        ArgumentNullException.ThrowIfNull(expenseTag);

        return new ExpenseTagMemorySnapshot(
            expenseTag.Id,
            expenseTag.Name,
            expenseTag.HexCode);
    }
}

public sealed record SavingGoalMemorySnapshot(
    int SavingGoalId,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateTime SavingEndDate,
    DateTime CreatedOn)
{
    public static SavingGoalMemorySnapshot Create(SavingGoal savingGoal)
    {
        ArgumentNullException.ThrowIfNull(savingGoal);

        return new SavingGoalMemorySnapshot(
            savingGoal.Id,
            savingGoal.Name,
            savingGoal.TargetAmount,
            savingGoal.CurrentAmount,
            savingGoal.SavingEndDate,
            savingGoal.CreatedOn);
    }
}

public sealed record UserSettingMemorySnapshot(
    string Name,
    string Value,
    bool Exists)
{
    public static UserSettingMemorySnapshot Create(UserSettings userSettings)
    {
        ArgumentNullException.ThrowIfNull(userSettings);

        return new UserSettingMemorySnapshot(
            userSettings.Name,
            userSettings.Value,
            true);
    }

    public static UserSettingMemorySnapshot Missing(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new UserSettingMemorySnapshot(name, string.Empty, false);
    }
}

public sealed record ExpenseLogMemorySnapshot(
    int ExpenseId,
    int ExpenseLogId,
    string ExpenseName,
    decimal Amount,
    ExpenseKind ExpenseKind,
    ExpenseCategory ExpenseCategory,
    int? RecurringDate,
    bool IsActive,
    int SpendingSourceId,
    int TagId,
    DateTime DeductedOn,
    string Notes,
    bool IsForDeletion)
{
    public static ExpenseLogMemorySnapshot Create(ExpenseLog expenseLog)
    {
        ArgumentNullException.ThrowIfNull(expenseLog);
        ArgumentNullException.ThrowIfNull(expenseLog.Expense);
        ArgumentNullException.ThrowIfNull(expenseLog.SpendingSource);
        ArgumentNullException.ThrowIfNull(expenseLog.Expense.ExpenseTag);

        return new ExpenseLogMemorySnapshot(
            expenseLog.Expense.Id,
            expenseLog.Id,
            expenseLog.Expense.Name,
            expenseLog.Amount,
            expenseLog.Expense.ExpenseKind,
            expenseLog.Expense.ExpenseCategory,
            expenseLog.Expense.RecurringDate,
            expenseLog.Expense.IsActive,
            expenseLog.SpendingSource.Id,
            expenseLog.Expense.ExpenseTag.Id,
            expenseLog.DeductedOn,
            expenseLog.Notes,
            expenseLog.IsForDeletion);
    }
}

public sealed record IncomeLogMemorySnapshot(
    int IncomeLogId,
    int SpendingSourceId,
    decimal Amount,
    DateTime AddedOn,
    string Notes)
{
    public static IncomeLogMemorySnapshot Create(IncomeLog incomeLog)
    {
        ArgumentNullException.ThrowIfNull(incomeLog);
        ArgumentNullException.ThrowIfNull(incomeLog.SpendingSource);

        return new IncomeLogMemorySnapshot(
            incomeLog.Id,
            incomeLog.SpendingSource.Id,
            incomeLog.Amount,
            incomeLog.AddedOn,
            incomeLog.Notes);
    }
}

public sealed class AddExpenseLogMemoryAction(
    ExpenseLogMemorySnapshot snapshot,
    bool shouldAdjustSpendingSourceTotals = true) : ILogMemoryAction
{
    public ExpenseLogMemorySnapshot Snapshot => snapshot;
    public bool ShouldAdjustSpendingSourceTotals => shouldAdjustSpendingSourceTotals;

    public string Description => "Add expense";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var expenseLog = await unitOfWork.ExpenseLogs.GetByIdAsync(snapshot.ExpenseLogId, cancellationToken);
        if (expenseLog is null)
            return;

        if (shouldAdjustSpendingSourceTotals)
        {
            LogMemoryPersistence.RevertExpenseFromSpendingSource(expenseLog.SpendingSource, expenseLog.Amount);
            unitOfWork.SpendingSources.Update(expenseLog.SpendingSource);
        }

        var expense = expenseLog.Expense;
        unitOfWork.ExpenseLogs.Remove(expenseLog);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (expense is null)
            return;

        var remainingExpenseLogs = await unitOfWork.ExpenseLogs.GetAllAsync(cancellationToken);
        if (remainingExpenseLogs.Any(log => log.Expense?.Id == expense.Id))
            return;

        var persistedExpense = await unitOfWork.Expenses.GetByIdAsync(expense.Id, cancellationToken);
        if (persistedExpense is null)
            return;

        unitOfWork.Expenses.Remove(persistedExpense);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.ExpenseLogs.GetByIdAsync(snapshot.ExpenseLogId, cancellationToken) is not null)
            return;

        if (shouldAdjustSpendingSourceTotals)
            await LogMemoryPersistence.GetRequiredSpendingSourceAsync(unitOfWork, snapshot.SpendingSourceId,
                cancellationToken);

        var expense = new Expense
        {
            Id = snapshot.ExpenseId,
            Name = snapshot.ExpenseName,
            Amount = snapshot.Amount,
            ExpenseKind = snapshot.ExpenseKind,
            ExpenseCategory = snapshot.ExpenseCategory,
            RecurringDate = snapshot.RecurringDate,
            IsActive = snapshot.IsActive,
            SpendingSourceId = snapshot.SpendingSourceId,
            ExpenseTagId = snapshot.TagId
        };

        var expenseLog = new ExpenseLog
        {
            Id = snapshot.ExpenseLogId,
            Expense = expense,
            Amount = snapshot.Amount,
            DeductedOn = snapshot.DeductedOn,
            Notes = snapshot.Notes,
            IsForDeletion = snapshot.IsForDeletion,
            SpendingSourceId = snapshot.SpendingSourceId
        };

        await unitOfWork.Expenses.AddAsync(expense, cancellationToken);
        await unitOfWork.ExpenseLogs.AddAsync(expenseLog, cancellationToken);

        if (shouldAdjustSpendingSourceTotals)
        {
            var spendingSource =
                await LogMemoryPersistence.GetRequiredSpendingSourceAsync(unitOfWork, snapshot.SpendingSourceId,
                    cancellationToken);
            LogMemoryPersistence.ApplyExpenseToSpendingSource(spendingSource, snapshot.Amount);
            unitOfWork.SpendingSources.Update(spendingSource);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AddIncomeLogMemoryAction(IncomeLogMemorySnapshot snapshot) : ILogMemoryAction
{
    public IncomeLogMemorySnapshot Snapshot => snapshot;

    public string Description => "Add income";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var incomeLog = await unitOfWork.IncomeLogs.GetByIdAsync(snapshot.IncomeLogId, cancellationToken);
        if (incomeLog is null)
            return;

        LogMemoryPersistence.RevertIncomeFromSpendingSource(incomeLog.SpendingSource, incomeLog.Amount);
        unitOfWork.SpendingSources.Update(incomeLog.SpendingSource);
        unitOfWork.IncomeLogs.Remove(incomeLog);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.IncomeLogs.GetByIdAsync(snapshot.IncomeLogId, cancellationToken) is not null)
            return;

        var spendingSource =
            await LogMemoryPersistence.GetRequiredSpendingSourceAsync(unitOfWork, snapshot.SpendingSourceId,
                cancellationToken);

        var incomeLog = new IncomeLog
        {
            Id = snapshot.IncomeLogId,
            Amount = snapshot.Amount,
            AddedOn = snapshot.AddedOn,
            Notes = snapshot.Notes,
            SpendingSourceId = snapshot.SpendingSourceId
        };

        await unitOfWork.IncomeLogs.AddAsync(incomeLog, cancellationToken);

        LogMemoryPersistence.ApplyIncomeToSpendingSource(spendingSource, snapshot.Amount);
        unitOfWork.SpendingSources.Update(spendingSource);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class CompositeLogMemoryAction(string description, IReadOnlyList<ILogMemoryAction> actions)
    : ILogMemoryAction
{
    public string Description { get; } = description;
    public IReadOnlyList<ILogMemoryAction> Actions { get; } = actions;

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        foreach (var action in actions.Reverse())
            await action.UndoAsync(unitOfWork, cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        foreach (var action in actions)
            await action.RedoAsync(unitOfWork, cancellationToken);
    }
}

public sealed class EditExpenseLogMemoryAction(
    ExpenseLogMemorySnapshot before,
    ExpenseLogMemorySnapshot after) : ILogMemoryAction
{
    public ExpenseLogMemorySnapshot Before => before;
    public ExpenseLogMemorySnapshot After => after;

    public string Description => "Edit expense";

    public Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, before, cancellationToken);
    }

    public Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, after, cancellationToken);
    }

    private static async Task ApplySnapshotAsync(IUnitOfWork unitOfWork, ExpenseLogMemorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var expenseLog = await unitOfWork.ExpenseLogs.GetByIdAsync(snapshot.ExpenseLogId, cancellationToken);
        if (expenseLog?.Expense is null)
            return;

        var currentSpendingSource = expenseLog.SpendingSource;
        var targetSpendingSource =
            await LogMemoryPersistence.GetRequiredSpendingSourceAsync(unitOfWork, snapshot.SpendingSourceId,
                cancellationToken);
        var expenseTag =
            await LogMemoryPersistence.GetRequiredExpenseTagAsync(unitOfWork, snapshot.TagId, cancellationToken);

        LogMemoryPersistence.RevertExpenseFromSpendingSource(currentSpendingSource, expenseLog.Amount);
        LogMemoryPersistence.ApplyExpenseToSpendingSource(targetSpendingSource, snapshot.Amount);

        expenseLog.Expense.Name = snapshot.ExpenseName;
        expenseLog.Expense.Amount = snapshot.Amount;
        expenseLog.Expense.ExpenseKind = snapshot.ExpenseKind;
        expenseLog.Expense.ExpenseCategory = snapshot.ExpenseCategory;
        expenseLog.Expense.RecurringDate = snapshot.RecurringDate;
        expenseLog.Expense.IsActive = snapshot.IsActive;
        expenseLog.Expense.SpendingSource = targetSpendingSource;
        expenseLog.Expense.ExpenseTag = expenseTag;

        expenseLog.Amount = snapshot.Amount;
        expenseLog.DeductedOn = snapshot.DeductedOn;
        expenseLog.Notes = snapshot.Notes;
        expenseLog.IsForDeletion = snapshot.IsForDeletion;
        expenseLog.SpendingSource = targetSpendingSource;

        unitOfWork.Expenses.Update(expenseLog.Expense);
        unitOfWork.ExpenseLogs.Update(expenseLog);
        unitOfWork.SpendingSources.Update(currentSpendingSource);

        if (!ReferenceEquals(currentSpendingSource, targetSpendingSource))
            unitOfWork.SpendingSources.Update(targetSpendingSource);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteExpenseLogMemoryAction : ILogMemoryAction
{
    private readonly int _expenseLogId;

    public DeleteExpenseLogMemoryAction(int expenseLogId)
    {
        _expenseLogId = expenseLogId;
    }

    public DeleteExpenseLogMemoryAction(ExpenseLogMemorySnapshot snapshot)
    {
        _expenseLogId = snapshot.ExpenseLogId;
        Snapshot = snapshot;
    }

    public int ExpenseLogId => _expenseLogId;
    public ExpenseLogMemorySnapshot? Snapshot { get; }

    public string Description => "Delete expense";

    public Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return SetDeletionStateAsync(unitOfWork, false, cancellationToken);
    }

    public Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return SetDeletionStateAsync(unitOfWork, true, cancellationToken);
    }

    private async Task SetDeletionStateAsync(IUnitOfWork unitOfWork, bool isForDeletion,
        CancellationToken cancellationToken)
    {
        var expenseLog = await unitOfWork.ExpenseLogs.GetByIdAsync(_expenseLogId, cancellationToken);
        if (expenseLog is null || expenseLog.IsForDeletion == isForDeletion)
            return;

        expenseLog.IsForDeletion = isForDeletion;
        unitOfWork.ExpenseLogs.Update(expenseLog);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class AddSpendingSourceMemoryAction(SpendingSourceMemorySnapshot snapshot) : ILogMemoryAction
{
    public string Description => "Add spending source";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var spendingSource =
            await unitOfWork.SpendingSources.GetByIdAsync(snapshot.SpendingSourceId, cancellationToken);
        if (spendingSource is null)
            return;

        unitOfWork.SpendingSources.Remove(spendingSource);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.SpendingSources.GetByIdAsync(snapshot.SpendingSourceId, cancellationToken) is not null)
            return;

        var spendingSource = new SpendingSource
        {
            Id = snapshot.SpendingSourceId,
            Name = snapshot.Name,
            SpendingSourceType = snapshot.SpendingSourceType,
            AccountLimit = snapshot.AccountLimit,
            SpentAmount = snapshot.SpentAmount,
            Balance = snapshot.Balance,
            MonthlyDueDate = snapshot.MonthlyDueDate,
            DeductSource = snapshot.DeductSource,
            InterestRate = snapshot.InterestRate,
            ShowOnUI = snapshot.ShowOnUI,
            IsEnabled = snapshot.IsEnabled
        };

        await unitOfWork.SpendingSources.AddAsync(spendingSource, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EditSpendingSourceMemoryAction(
    SpendingSourceMemorySnapshot before,
    SpendingSourceMemorySnapshot after) : ILogMemoryAction
{
    public string Description => "Edit spending source";

    public Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, before, cancellationToken);
    }

    public Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, after, cancellationToken);
    }

    private static async Task ApplySnapshotAsync(IUnitOfWork unitOfWork, SpendingSourceMemorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var spendingSource =
            await LogMemoryPersistence.GetRequiredSpendingSourceAsync(unitOfWork, snapshot.SpendingSourceId,
                cancellationToken);

        spendingSource.Name = snapshot.Name;
        spendingSource.SpendingSourceType = snapshot.SpendingSourceType;
        spendingSource.AccountLimit = snapshot.AccountLimit;
        spendingSource.SpentAmount = snapshot.SpentAmount;
        spendingSource.Balance = snapshot.Balance;
        spendingSource.MonthlyDueDate = snapshot.MonthlyDueDate;
        spendingSource.DeductSource = snapshot.DeductSource;
        spendingSource.InterestRate = snapshot.InterestRate;
        spendingSource.ShowOnUI = snapshot.ShowOnUI;
        spendingSource.IsEnabled = snapshot.IsEnabled;

        unitOfWork.SpendingSources.Update(spendingSource);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteSpendingSourceMemoryAction(SpendingSourceMemorySnapshot snapshot) : ILogMemoryAction
{
    public string Description => "Delete spending source";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.SpendingSources.GetByIdAsync(snapshot.SpendingSourceId, cancellationToken) is not null)
            return;

        var spendingSource = new SpendingSource
        {
            Id = snapshot.SpendingSourceId,
            Name = snapshot.Name,
            SpendingSourceType = snapshot.SpendingSourceType,
            AccountLimit = snapshot.AccountLimit,
            SpentAmount = snapshot.SpentAmount,
            Balance = snapshot.Balance,
            MonthlyDueDate = snapshot.MonthlyDueDate,
            DeductSource = snapshot.DeductSource,
            InterestRate = snapshot.InterestRate,
            ShowOnUI = snapshot.ShowOnUI,
            IsEnabled = snapshot.IsEnabled
        };

        await unitOfWork.SpendingSources.AddAsync(spendingSource, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var spendingSource =
            await unitOfWork.SpendingSources.GetByIdAsync(snapshot.SpendingSourceId, cancellationToken);
        if (spendingSource is null)
            return;

        unitOfWork.SpendingSources.Remove(spendingSource);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EditExpenseMemoryAction(
    ExpenseMemorySnapshot before,
    ExpenseMemorySnapshot after) : ILogMemoryAction
{
    public string Description => "Edit fixed expense";

    public Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, before, cancellationToken);
    }

    public Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, after, cancellationToken);
    }

    private static async Task ApplySnapshotAsync(IUnitOfWork unitOfWork, ExpenseMemorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var expense = await unitOfWork.Expenses.GetByIdAsync(snapshot.ExpenseId, cancellationToken);
        if (expense is null)
            return;

        expense.Name = snapshot.Name;
        expense.Amount = snapshot.Amount;
        expense.ExpenseKind = snapshot.ExpenseKind;
        expense.ExpenseCategory = snapshot.ExpenseCategory;
        expense.RecurringDate = snapshot.RecurringDate;
        expense.IsActive = snapshot.IsActive;
        expense.SpendingSourceId = snapshot.SpendingSourceId;
        expense.ExpenseTagId = snapshot.TagId;

        unitOfWork.Expenses.Update(expense);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteExpenseMemoryAction(ExpenseMemorySnapshot snapshot) : ILogMemoryAction
{
    public string Description => "Delete fixed expense";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.Expenses.GetByIdAsync(snapshot.ExpenseId, cancellationToken) is not null)
            return;

        var spendingSource =
            await LogMemoryPersistence.GetRequiredSpendingSourceAsync(unitOfWork, snapshot.SpendingSourceId,
                cancellationToken);
        var expenseTag = await LogMemoryPersistence.GetRequiredExpenseTagAsync(unitOfWork, snapshot.TagId,
            cancellationToken);

        var expense = new Expense
        {
            Id = snapshot.ExpenseId,
            Name = snapshot.Name,
            Amount = snapshot.Amount,
            ExpenseKind = snapshot.ExpenseKind,
            ExpenseCategory = snapshot.ExpenseCategory,
            RecurringDate = snapshot.RecurringDate,
            IsActive = snapshot.IsActive,
            SpendingSourceId = snapshot.SpendingSourceId,
            ExpenseTagId = snapshot.TagId,
            SpendingSource = spendingSource,
            ExpenseTag = expenseTag
        };

        await unitOfWork.Expenses.AddAsync(expense, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var expense = await unitOfWork.Expenses.GetByIdAsync(snapshot.ExpenseId, cancellationToken);
        if (expense is null)
            return;

        unitOfWork.Expenses.Remove(expense);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteExpenseTagMemoryAction(ExpenseTagMemorySnapshot snapshot) : ILogMemoryAction
{
    public string Description => "Delete tag";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.ExpenseTags.GetByIdAsync(snapshot.ExpenseTagId, cancellationToken) is not null)
            return;

        var expenseTag = new ExpenseTag
        {
            Id = snapshot.ExpenseTagId,
            Name = snapshot.Name,
            HexCode = snapshot.HexCode
        };

        await unitOfWork.ExpenseTags.AddAsync(expenseTag, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var expenseTag = await unitOfWork.ExpenseTags.GetByIdAsync(snapshot.ExpenseTagId, cancellationToken);
        if (expenseTag is null)
            return;

        unitOfWork.ExpenseTags.Remove(expenseTag);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteSavingGoalMemoryAction(SavingGoalMemorySnapshot snapshot) : ILogMemoryAction
{
    public string Description => "Delete saving goal";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.SavingGoals.GetByIdAsync(snapshot.SavingGoalId, cancellationToken) is not null)
            return;

        var savingGoal = new SavingGoal
        {
            Id = snapshot.SavingGoalId,
            Name = snapshot.Name,
            TargetAmount = snapshot.TargetAmount,
            CurrentAmount = snapshot.CurrentAmount,
            SavingEndDate = snapshot.SavingEndDate,
            CreatedOn = snapshot.CreatedOn
        };

        await unitOfWork.SavingGoals.AddAsync(savingGoal, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var savingGoal = await unitOfWork.SavingGoals.GetByIdAsync(snapshot.SavingGoalId, cancellationToken);
        if (savingGoal is null)
            return;

        unitOfWork.SavingGoals.Remove(savingGoal);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class SetUserSettingMemoryAction(
    UserSettingMemorySnapshot before,
    UserSettingMemorySnapshot after) : ILogMemoryAction
{
    public string Description => "Update setting";

    public Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, before, cancellationToken);
    }

    public Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, after, cancellationToken);
    }

    private static async Task ApplySnapshotAsync(IUnitOfWork unitOfWork, UserSettingMemorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var existingSetting = await unitOfWork.UserSettings.GetByNameAsync(snapshot.Name, cancellationToken);

        if (!snapshot.Exists)
        {
            if (existingSetting is null)
                return;

            unitOfWork.UserSettings.Remove(existingSetting);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        if (existingSetting is null)
        {
            await unitOfWork.UserSettings.AddAsync(new UserSettings
            {
                Name = snapshot.Name,
                Value = snapshot.Value
            }, cancellationToken);
        }
        else
        {
            existingSetting.Value = snapshot.Value;
            unitOfWork.UserSettings.Update(existingSetting);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

internal static class LogMemoryPersistence
{
    internal static void ApplyExpenseToSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount += amount;
            return;
        }

        spendingSource.Balance -= amount;
    }

    internal static void RevertExpenseFromSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount = Math.Max(0m, spendingSource.SpentAmount - amount);
            return;
        }

        spendingSource.Balance += amount;
    }

    internal static void ApplyIncomeToSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount = Math.Max(0m, spendingSource.SpentAmount - amount);
            return;
        }

        spendingSource.Balance += amount;
    }

    internal static void RevertIncomeFromSpendingSource(SpendingSource spendingSource, decimal amount)
    {
        if (spendingSource.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            spendingSource.SpentAmount += amount;
            return;
        }

        spendingSource.Balance -= amount;
    }

    internal static async Task<SpendingSource> GetRequiredSpendingSourceAsync(IUnitOfWork unitOfWork,
        int spendingSourceId, CancellationToken cancellationToken)
    {
        var spendingSource = await unitOfWork.SpendingSources.GetByIdAsync(spendingSourceId, cancellationToken);
        return spendingSource ??
               throw new InvalidOperationException($"Unable to find spending source {spendingSourceId}.");
    }

    internal static async Task<ExpenseTag> GetRequiredExpenseTagAsync(IUnitOfWork unitOfWork, int expenseTagId,
        CancellationToken cancellationToken)
    {
        var expenseTag = await unitOfWork.ExpenseTags.GetByIdAsync(expenseTagId, cancellationToken);
        return expenseTag ?? throw new InvalidOperationException($"Unable to find expense tag {expenseTagId}.");
    }
}
