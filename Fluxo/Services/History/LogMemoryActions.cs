using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.History;
using Fluxo.Core.Interfaces;

namespace Fluxo.Services.History;

public interface ILogMemoryAction : Fluxo.Core.Interfaces.History.ILogMemoryAction;

public sealed record AccountMemorySnapshot(
    int AccountId,
    string Name,
    AccountType AccountType,
    decimal AccountLimit,
    decimal MaximumSpending,
    decimal? MinimumPayment,
    decimal SpentAmount,
    decimal Balance,
    int? MonthlyDueDate,
    int? DeductSource,
    decimal? InterestRate,
    bool PinnedOnUI,
    bool IsEnabled)
{
    public static AccountMemorySnapshot Create(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);

        return new AccountMemorySnapshot(
            account.Id,
            account.Name,
            account.AccountType,
            account.AccountLimit,
            account.MaximumSpending,
            account.MinimumPayment,
            account.SpentAmount,
            account.Balance,
            account.MonthlyDueDate,
            account.DeductSource,
            account.InterestRate,
            account.PinnedOnUI,
            account.IsEnabled);
    }
}

public sealed record ExpenseMemorySnapshot(
    int ExpenseId,
    int AccountId,
    int TagId,
    string Name,
    decimal Amount,
    ExpenseCategory ExpenseCategory,
    bool IsLend = false)
{
    public static ExpenseMemorySnapshot Create(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        return new ExpenseMemorySnapshot(
            expense.Id,
            expense.AccountId,
            expense.ExpenseTagId,
            expense.Name,
            expense.Amount,
            expense.ExpenseCategory,
            expense.IsLend);
    }
}

public sealed record ExpenseTagMemorySnapshot(
    int ExpenseTagId,
    string Name,
    string HexCode,
    decimal? SpendingLimit)
{
    public static ExpenseTagMemorySnapshot Create(ExpenseTag expenseTag)
    {
        ArgumentNullException.ThrowIfNull(expenseTag);

        return new ExpenseTagMemorySnapshot(
            expenseTag.Id,
            expenseTag.Name,
            expenseTag.HexCode,
            expenseTag.SpendingLimit);
    }
}

public sealed record SavingGoalMemorySnapshot(
    int SavingGoalId,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    DateTime? SavingEndDate,
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
    ExpenseCategory ExpenseCategory,
    int AccountId,
    int TagId,
    DateTime DeductedOn,
    string Notes,
    bool IsForDeletion,
    int? ParentLogId,
    bool IsLend = false)
{
    public static ExpenseLogMemorySnapshot Create(ExpenseLog expenseLog)
    {
        ArgumentNullException.ThrowIfNull(expenseLog);
        ArgumentNullException.ThrowIfNull(expenseLog.Expense);
        ArgumentNullException.ThrowIfNull(expenseLog.Account);
        ArgumentNullException.ThrowIfNull(expenseLog.Expense.ExpenseTag);

        return new ExpenseLogMemorySnapshot(
            expenseLog.Expense.Id,
            expenseLog.Id,
            expenseLog.Expense.Name,
            expenseLog.Amount,
            expenseLog.Expense.ExpenseCategory,
            expenseLog.Account.Id,
            expenseLog.Expense.ExpenseTag.Id,
            expenseLog.DeductedOn,
            expenseLog.Notes,
            expenseLog.IsForDeletion,
            expenseLog.ParentLogId,
            expenseLog.IsLend || expenseLog.Expense.IsLend);
    }
}

public sealed record IncomeLogMemorySnapshot(
    int IncomeLogId,
    int AccountId,
    string Name,
    decimal Amount,
    DateTime AddedOn,
    string Notes,
    bool IsDebt = false)
{
    public static IncomeLogMemorySnapshot Create(IncomeLog incomeLog)
    {
        ArgumentNullException.ThrowIfNull(incomeLog);
        ArgumentNullException.ThrowIfNull(incomeLog.Account);

        return new IncomeLogMemorySnapshot(
            incomeLog.Id,
            incomeLog.Account.Id,
            incomeLog.Name,
            incomeLog.Amount,
            incomeLog.AddedOn,
            incomeLog.Notes,
            incomeLog.IsDebt);
    }
}

public sealed class AddExpenseLogMemoryAction(
    ExpenseLogMemorySnapshot snapshot,
    bool shouldAdjustAccountTotals = true) : ILogMemoryAction
{
    public ExpenseLogMemorySnapshot Snapshot => snapshot;
    public bool ShouldAdjustAccountTotals => shouldAdjustAccountTotals;

    public string Description => "Add expense";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var expenseLog = await unitOfWork.ExpenseLogs.GetByIdAsync(snapshot.ExpenseLogId, cancellationToken);
        if (expenseLog is null)
            return;

        if (shouldAdjustAccountTotals)
        {
            LogMemoryPersistence.RevertExpenseFromAccount(expenseLog.Account, expenseLog.Amount);
            unitOfWork.Accounts.Update(expenseLog.Account);
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

        if (shouldAdjustAccountTotals)
            await LogMemoryPersistence.GetRequiredAccountAsync(unitOfWork, snapshot.AccountId,
                cancellationToken);

        var expense = new Expense
        {
            Id = snapshot.ExpenseId,
            Name = snapshot.ExpenseName,
            Amount = snapshot.Amount,
            ExpenseCategory = snapshot.ExpenseCategory,
            AccountId = snapshot.AccountId,
            ExpenseTagId = snapshot.TagId,
            IsLend = snapshot.IsLend
        };

        var expenseLog = new ExpenseLog
        {
            Id = snapshot.ExpenseLogId,
            Expense = expense,
            Amount = snapshot.Amount,
            DeductedOn = snapshot.DeductedOn,
            Notes = snapshot.Notes,
            IsForDeletion = snapshot.IsForDeletion,
            AccountId = snapshot.AccountId,
            ParentLogId = snapshot.ParentLogId,
            IsLend = snapshot.IsLend
        };

        await unitOfWork.Expenses.AddAsync(expense, cancellationToken);
        await unitOfWork.ExpenseLogs.AddAsync(expenseLog, cancellationToken);

        if (shouldAdjustAccountTotals)
        {
            var account =
                await LogMemoryPersistence.GetRequiredAccountAsync(unitOfWork, snapshot.AccountId,
                    cancellationToken);
            LogMemoryPersistence.ApplyExpenseToAccount(account, snapshot.Amount);
            unitOfWork.Accounts.Update(account);
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

        LogMemoryPersistence.RevertIncomeFromAccount(incomeLog.Account, incomeLog.Amount);
        unitOfWork.Accounts.Update(incomeLog.Account);
        unitOfWork.IncomeLogs.Remove(incomeLog);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.IncomeLogs.GetByIdAsync(snapshot.IncomeLogId, cancellationToken) is not null)
            return;

        var account =
            await LogMemoryPersistence.GetRequiredAccountAsync(unitOfWork, snapshot.AccountId,
                cancellationToken);

        var incomeLog = new IncomeLog
        {
            Id = snapshot.IncomeLogId,
            Name = snapshot.Name,
            Amount = snapshot.Amount,
            AddedOn = snapshot.AddedOn,
            Notes = snapshot.Notes,
            AccountId = snapshot.AccountId,
            IsDebt = snapshot.IsDebt
        };

        await unitOfWork.IncomeLogs.AddAsync(incomeLog, cancellationToken);

        LogMemoryPersistence.ApplyIncomeToAccount(account, snapshot.Amount);
        unitOfWork.Accounts.Update(account);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EditIncomeLogMemoryAction(
    IncomeLogMemorySnapshot before,
    IncomeLogMemorySnapshot after) : ILogMemoryAction
{
    public IncomeLogMemorySnapshot Before => before;
    public IncomeLogMemorySnapshot After => after;

    public string Description => "Edit income";

    public Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, before, cancellationToken);
    }

    public Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, after, cancellationToken);
    }

    private static async Task ApplySnapshotAsync(IUnitOfWork unitOfWork, IncomeLogMemorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var incomeLog = await unitOfWork.IncomeLogs.GetByIdAsync(snapshot.IncomeLogId, cancellationToken);
        if (incomeLog is null)
            return;

        var currentAccount = incomeLog.Account;
        var targetAccount =
            await LogMemoryPersistence.GetRequiredAccountAsync(unitOfWork, snapshot.AccountId,
                cancellationToken);

        LogMemoryPersistence.RevertIncomeFromAccount(currentAccount, incomeLog.Amount);
        LogMemoryPersistence.ApplyIncomeToAccount(targetAccount, snapshot.Amount);

        incomeLog.Name = snapshot.Name;
        incomeLog.Amount = snapshot.Amount;
        incomeLog.AddedOn = snapshot.AddedOn;
        incomeLog.Notes = snapshot.Notes;
        incomeLog.IsDebt = snapshot.IsDebt;
        incomeLog.Account = targetAccount;
        incomeLog.AccountId = snapshot.AccountId;

        unitOfWork.IncomeLogs.Update(incomeLog);
        unitOfWork.Accounts.Update(currentAccount);

        if (!ReferenceEquals(currentAccount, targetAccount))
            unitOfWork.Accounts.Update(targetAccount);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteIncomeLogMemoryAction(IncomeLogMemorySnapshot snapshot) : ILogMemoryAction
{
    public IncomeLogMemorySnapshot Snapshot => snapshot;

    public string Description => "Delete income";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.IncomeLogs.GetByIdAsync(snapshot.IncomeLogId, cancellationToken) is not null)
            return;

        var account =
            await LogMemoryPersistence.GetRequiredAccountAsync(unitOfWork, snapshot.AccountId,
                cancellationToken);

        var incomeLog = new IncomeLog
        {
            Id = snapshot.IncomeLogId,
            Name = snapshot.Name,
            Amount = snapshot.Amount,
            AddedOn = snapshot.AddedOn,
            Notes = snapshot.Notes,
            AccountId = snapshot.AccountId,
            Account = account,
            IsDebt = snapshot.IsDebt
        };

        await unitOfWork.IncomeLogs.AddAsync(incomeLog, cancellationToken);
        LogMemoryPersistence.ApplyIncomeToAccount(account, snapshot.Amount);
        unitOfWork.Accounts.Update(account);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var incomeLog = await unitOfWork.IncomeLogs.GetByIdAsync(snapshot.IncomeLogId, cancellationToken);
        if (incomeLog is null)
            return;

        LogMemoryPersistence.RevertIncomeFromAccount(incomeLog.Account, incomeLog.Amount);
        unitOfWork.Accounts.Update(incomeLog.Account);
        unitOfWork.IncomeLogs.Remove(incomeLog);

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

        var currentAccount = expenseLog.Account;
        var targetAccount =
            await LogMemoryPersistence.GetRequiredAccountAsync(unitOfWork, snapshot.AccountId,
                cancellationToken);
        var expenseTag =
            await LogMemoryPersistence.GetRequiredExpenseTagAsync(unitOfWork, snapshot.TagId, cancellationToken);

        LogMemoryPersistence.RevertExpenseFromAccount(currentAccount, expenseLog.Amount);
        LogMemoryPersistence.ApplyExpenseToAccount(targetAccount, snapshot.Amount);

        expenseLog.Expense.Name = snapshot.ExpenseName;
        expenseLog.Expense.Amount = snapshot.Amount;
        expenseLog.Expense.ExpenseCategory = snapshot.ExpenseCategory;
        expenseLog.Expense.Account = targetAccount;
        expenseLog.Expense.ExpenseTag = expenseTag;
        expenseLog.Expense.IsLend = snapshot.IsLend;

        expenseLog.Amount = snapshot.Amount;
        expenseLog.DeductedOn = snapshot.DeductedOn;
        expenseLog.Notes = snapshot.Notes;
        expenseLog.IsForDeletion = snapshot.IsForDeletion;
        expenseLog.IsLend = snapshot.IsLend;
        expenseLog.Account = targetAccount;
        expenseLog.ParentLogId = snapshot.ParentLogId;

        unitOfWork.Expenses.Update(expenseLog.Expense);
        unitOfWork.ExpenseLogs.Update(expenseLog);
        unitOfWork.Accounts.Update(currentAccount);

        if (!ReferenceEquals(currentAccount, targetAccount))
            unitOfWork.Accounts.Update(targetAccount);

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

public sealed class AddAccountMemoryAction(AccountMemorySnapshot snapshot) : ILogMemoryAction
{
    public string Description => "Add account";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var account =
            await unitOfWork.Accounts.GetByIdAsync(snapshot.AccountId, cancellationToken);
        if (account is null)
            return;

        unitOfWork.Accounts.Remove(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.Accounts.GetByIdAsync(snapshot.AccountId, cancellationToken) is not null)
            return;

        var account = new Account
        {
            Id = snapshot.AccountId,
            Name = snapshot.Name,
            AccountType = snapshot.AccountType,
            AccountLimit = snapshot.AccountLimit,
            MaximumSpending = snapshot.MaximumSpending,
            MinimumPayment = snapshot.MinimumPayment,
            SpentAmount = snapshot.SpentAmount,
            Balance = snapshot.Balance,
            MonthlyDueDate = snapshot.MonthlyDueDate,
            DeductSource = snapshot.DeductSource,
            InterestRate = snapshot.InterestRate,
            PinnedOnUI = snapshot.PinnedOnUI,
            IsEnabled = snapshot.IsEnabled
        };

        await unitOfWork.Accounts.AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EditAccountMemoryAction(
    AccountMemorySnapshot before,
    AccountMemorySnapshot after) : ILogMemoryAction
{
    public string Description => "Edit account";

    public Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, before, cancellationToken);
    }

    public Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, after, cancellationToken);
    }

    private static async Task ApplySnapshotAsync(IUnitOfWork unitOfWork, AccountMemorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var account =
            await LogMemoryPersistence.GetRequiredAccountAsync(unitOfWork, snapshot.AccountId,
                cancellationToken);

        account.Name = snapshot.Name;
        account.AccountType = snapshot.AccountType;
        account.AccountLimit = snapshot.AccountLimit;
        account.MaximumSpending = snapshot.MaximumSpending;
        account.MinimumPayment = snapshot.MinimumPayment;
        account.SpentAmount = snapshot.SpentAmount;
        account.Balance = snapshot.Balance;
        account.MonthlyDueDate = snapshot.MonthlyDueDate;
        account.DeductSource = snapshot.DeductSource;
        account.InterestRate = snapshot.InterestRate;
        account.PinnedOnUI = snapshot.PinnedOnUI;
        account.IsEnabled = snapshot.IsEnabled;

        unitOfWork.Accounts.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteAccountMemoryAction(AccountMemorySnapshot snapshot) : ILogMemoryAction
{
    public string Description => "Delete account";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.Accounts.GetByIdAsync(snapshot.AccountId, cancellationToken) is not null)
            return;

        var account = new Account
        {
            Id = snapshot.AccountId,
            Name = snapshot.Name,
            AccountType = snapshot.AccountType,
            AccountLimit = snapshot.AccountLimit,
            MaximumSpending = snapshot.MaximumSpending,
            MinimumPayment = snapshot.MinimumPayment,
            SpentAmount = snapshot.SpentAmount,
            Balance = snapshot.Balance,
            MonthlyDueDate = snapshot.MonthlyDueDate,
            DeductSource = snapshot.DeductSource,
            InterestRate = snapshot.InterestRate,
            PinnedOnUI = snapshot.PinnedOnUI,
            IsEnabled = snapshot.IsEnabled
        };

        await unitOfWork.Accounts.AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var account =
            await unitOfWork.Accounts.GetByIdAsync(snapshot.AccountId, cancellationToken);
        if (account is null)
            return;

        unitOfWork.Accounts.Remove(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EditExpenseMemoryAction(
    ExpenseMemorySnapshot before,
    ExpenseMemorySnapshot after) : ILogMemoryAction
{
    public string Description => "Edit recurring transaction";

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
        expense.ExpenseCategory = snapshot.ExpenseCategory;
        expense.AccountId = snapshot.AccountId;
        expense.ExpenseTagId = snapshot.TagId;
        expense.IsLend = snapshot.IsLend;

        unitOfWork.Expenses.Update(expense);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteExpenseMemoryAction(ExpenseMemorySnapshot snapshot) : ILogMemoryAction
{
    public string Description => "Delete recurring transaction";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.Expenses.GetByIdAsync(snapshot.ExpenseId, cancellationToken) is not null)
            return;

        var account =
            await LogMemoryPersistence.GetRequiredAccountAsync(unitOfWork, snapshot.AccountId,
                cancellationToken);
        var expenseTag = await LogMemoryPersistence.GetRequiredExpenseTagAsync(unitOfWork, snapshot.TagId,
            cancellationToken);

        var expense = new Expense
        {
            Id = snapshot.ExpenseId,
            Name = snapshot.Name,
            Amount = snapshot.Amount,
            ExpenseCategory = snapshot.ExpenseCategory,
            AccountId = snapshot.AccountId,
            ExpenseTagId = snapshot.TagId,
            Account = account,
            ExpenseTag = expenseTag,
            IsLend = snapshot.IsLend
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
            HexCode = snapshot.HexCode,
            SpendingLimit = snapshot.SpendingLimit
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

public sealed class EditExpenseTagMemoryAction(
    ExpenseTagMemorySnapshot before,
    ExpenseTagMemorySnapshot after) : ILogMemoryAction
{
    public string Description => "Edit tag";

    public Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, before, cancellationToken);
    }

    public Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        return ApplySnapshotAsync(unitOfWork, after, cancellationToken);
    }

    private static async Task ApplySnapshotAsync(IUnitOfWork unitOfWork, ExpenseTagMemorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var expenseTag = await unitOfWork.ExpenseTags.GetByIdAsync(snapshot.ExpenseTagId, cancellationToken);
        if (expenseTag is null)
            return;

        expenseTag.Name = snapshot.Name;
        expenseTag.HexCode = snapshot.HexCode;
        expenseTag.SpendingLimit = snapshot.SpendingLimit;
        unitOfWork.ExpenseTags.Update(expenseTag);
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
    internal static void ApplyExpenseToAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount += amount;
            return;
        }

        account.Balance -= amount;
    }

    internal static void RevertExpenseFromAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount = Math.Max(0m, account.SpentAmount - amount);
            return;
        }

        account.Balance += amount;
    }

    internal static void ApplyIncomeToAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount = Math.Max(0m, account.SpentAmount - amount);
            return;
        }

        account.Balance += amount;
    }

    internal static void RevertIncomeFromAccount(Account account, decimal amount)
    {
        if (account.AccountType == AccountType.Credit)
        {
            account.SpentAmount += amount;
            return;
        }

        account.Balance -= amount;
    }

    internal static async Task<Account> GetRequiredAccountAsync(IUnitOfWork unitOfWork,
        int accountId, CancellationToken cancellationToken)
    {
        var account = await unitOfWork.Accounts.GetByIdAsync(accountId, cancellationToken);
        return account ??
               throw new InvalidOperationException($"Unable to find account {accountId}.");
    }

    internal static async Task<ExpenseTag> GetRequiredExpenseTagAsync(IUnitOfWork unitOfWork, int expenseTagId,
        CancellationToken cancellationToken)
    {
        var expenseTag = await unitOfWork.ExpenseTags.GetByIdAsync(expenseTagId, cancellationToken);
        return expenseTag ?? throw new InvalidOperationException($"Unable to find expense tag {expenseTagId}.");
    }
}
