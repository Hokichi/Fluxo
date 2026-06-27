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
    bool IsEnabled,
    bool IsDefault = false)
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
            account.IsEnabled,
            account.IsDefault);
    }
}

public sealed record ExpenseMemorySnapshot(
    int ExpenseId,
    int AccountId,
    int TagId,
    string Name,
    decimal Amount,
    ExpenseCategory ExpenseCategory,
    bool IsIoU = false)
{
    public static ExpenseMemorySnapshot Create(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        return new ExpenseMemorySnapshot(
            expense.Id,
            expense.AccountId,
            expense.TagId,
            expense.Name,
            expense.Amount,
            expense.ExpenseCategory,
            expense.IsIoU);
    }
}

public sealed record TagMemorySnapshot(
    int TagId,
    string Name,
    string HexCode,
    decimal? SpendingLimit)
{
    public static TagMemorySnapshot Create(Tag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        return new TagMemorySnapshot(
            tag.Id,
            tag.Name,
            tag.HexCode,
            tag.SpendingLimit);
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
    bool IsIoU = false)
{
    public static ExpenseLogMemorySnapshot Create(ExpenseLog expenseLog)
    {
        ArgumentNullException.ThrowIfNull(expenseLog);
        ArgumentNullException.ThrowIfNull(expenseLog.Expense);
        ArgumentNullException.ThrowIfNull(expenseLog.Account);
        ArgumentNullException.ThrowIfNull(expenseLog.Expense.Tag);

        return new ExpenseLogMemorySnapshot(
            expenseLog.Expense.Id,
            expenseLog.Id,
            expenseLog.Expense.Name,
            expenseLog.Amount,
            expenseLog.Expense.ExpenseCategory,
            expenseLog.Account.Id,
            expenseLog.Expense.Tag.Id,
            expenseLog.DeductedOn,
            expenseLog.Notes,
            expenseLog.IsForDeletion,
            expenseLog.ParentLogId,
            expenseLog.IsIoU || expenseLog.Expense.IsIoU);
    }
}

public sealed record IncomeLogMemorySnapshot(
    int IncomeLogId,
    int AccountId,
    string Name,
    decimal Amount,
    DateTime AddedOn,
    string Notes,
    bool IsIoU = false)
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
            incomeLog.IsIoU);
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
            TagId = snapshot.TagId,
            IsIoU = snapshot.IsIoU
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
            IsIoU = snapshot.IsIoU
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
            IsIoU = snapshot.IsIoU
        };

        await unitOfWork.IncomeLogs.AddAsync(incomeLog, cancellationToken);

        LogMemoryPersistence.ApplyIncomeToAccount(account, snapshot.Amount);
        unitOfWork.Accounts.Update(account);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed record TransactionMemorySnapshot(
    int TransactionId,
    TransactionType Type,
    int AccountId,
    string Name,
    decimal Amount,
    DateTime OccurredOn,
    string Notes,
    ExpenseCategory? ExpenseCategory,
    int? TagId,
    int? ParentTransactionId,
    bool IsPinned,
    bool IsForDeletion,
    bool IsIoU,
    bool IsExcludedFromBudget)
{
    public static TransactionMemorySnapshot Create(Transaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        return new TransactionMemorySnapshot(
            transaction.Id,
            transaction.Type,
            transaction.AccountId,
            transaction.Name,
            transaction.Amount,
            transaction.OccurredOn,
            transaction.Notes,
            transaction.ExpenseCategory,
            transaction.TagId,
            transaction.ParentTransactionId,
            transaction.IsPinned,
            transaction.IsForDeletion,
            transaction.IsIoU,
            transaction.IsExcludedFromBudget);
    }
}

public sealed class AddTransactionMemoryAction(
    TransactionMemorySnapshot snapshot,
    bool shouldAdjustAccountTotals = true) : ILogMemoryAction
{
    public TransactionMemorySnapshot Snapshot => snapshot;
    public string Description => snapshot.Type == TransactionType.Expense ? "Add expense" : "Add income";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var transaction = await unitOfWork.Transactions.GetByIdAsync(snapshot.TransactionId, cancellationToken);
        if (transaction is null)
            return;

        if (shouldAdjustAccountTotals)
        {
            LogMemoryPersistence.RevertTransactionFromAccount(transaction.Account, transaction.Type, transaction.Amount);
            unitOfWork.Accounts.Update(transaction.Account);
        }
        unitOfWork.Transactions.Remove(transaction);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.Transactions.GetByIdAsync(snapshot.TransactionId, cancellationToken) is not null)
            return;

        var account = await LogMemoryPersistence.GetRequiredAccountAsync(
            unitOfWork, snapshot.AccountId, cancellationToken);
        var transaction = CreateTransaction(snapshot, account);
        await unitOfWork.Transactions.AddAsync(transaction, cancellationToken);
        if (shouldAdjustAccountTotals)
        {
            LogMemoryPersistence.ApplyTransactionToAccount(account, snapshot.Type, snapshot.Amount);
            unitOfWork.Accounts.Update(account);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    internal static Transaction CreateTransaction(TransactionMemorySnapshot value, Account account) => new()
    {
        Id = value.TransactionId,
        Type = value.Type,
        AccountId = value.AccountId,
        Account = account,
        Name = value.Name,
        Amount = value.Amount,
        OccurredOn = value.OccurredOn,
        Notes = value.Notes,
        ExpenseCategory = value.ExpenseCategory,
        TagId = value.TagId,
        ParentTransactionId = value.ParentTransactionId,
        IsPinned = value.IsPinned,
        IsForDeletion = value.IsForDeletion,
        IsIoU = value.IsIoU,
        IsExcludedFromBudget = value.IsExcludedFromBudget
    };
}

public sealed class EditTransactionMemoryAction(
    TransactionMemorySnapshot before,
    TransactionMemorySnapshot after) : ILogMemoryAction
{
    public TransactionMemorySnapshot Before => before;
    public TransactionMemorySnapshot After => after;
    public string Description => "Edit transaction";
    public Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default) =>
        ApplyAsync(unitOfWork, before, cancellationToken);
    public Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default) =>
        ApplyAsync(unitOfWork, after, cancellationToken);

    private static async Task ApplyAsync(IUnitOfWork unitOfWork, TransactionMemorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var transaction = await unitOfWork.Transactions.GetByIdAsync(snapshot.TransactionId, cancellationToken);
        if (transaction is null)
            return;

        var oldAccount = transaction.Account;
        var newAccount = await LogMemoryPersistence.GetRequiredAccountAsync(
            unitOfWork, snapshot.AccountId, cancellationToken);
        LogMemoryPersistence.RevertTransactionFromAccount(oldAccount, transaction.Type, transaction.Amount);
        LogMemoryPersistence.ApplyTransactionToAccount(newAccount, snapshot.Type, snapshot.Amount);

        transaction.Type = snapshot.Type;
        transaction.AccountId = snapshot.AccountId;
        transaction.Account = newAccount;
        transaction.Name = snapshot.Name;
        transaction.Amount = snapshot.Amount;
        transaction.OccurredOn = snapshot.OccurredOn;
        transaction.Notes = snapshot.Notes;
        transaction.ExpenseCategory = snapshot.ExpenseCategory;
        transaction.TagId = snapshot.TagId;
        transaction.ParentTransactionId = snapshot.ParentTransactionId;
        transaction.IsPinned = snapshot.IsPinned;
        transaction.IsForDeletion = snapshot.IsForDeletion;
        transaction.IsIoU = snapshot.IsIoU;
        transaction.IsExcludedFromBudget = snapshot.IsExcludedFromBudget;

        unitOfWork.Transactions.Update(transaction);
        unitOfWork.Accounts.Update(oldAccount);
        if (!ReferenceEquals(oldAccount, newAccount))
            unitOfWork.Accounts.Update(newAccount);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class DeleteTransactionMemoryAction(TransactionMemorySnapshot snapshot) : ILogMemoryAction
{
    public TransactionMemorySnapshot Snapshot => snapshot;
    public string Description => "Delete transaction";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.Transactions.GetByIdAsync(snapshot.TransactionId, cancellationToken) is not null)
            return;

        var account = await LogMemoryPersistence.GetRequiredAccountAsync(
            unitOfWork, snapshot.AccountId, cancellationToken);
        await unitOfWork.Transactions.AddAsync(AddTransactionMemoryAction.CreateTransaction(snapshot, account), cancellationToken);
        LogMemoryPersistence.ApplyTransactionToAccount(account, snapshot.Type, snapshot.Amount);
        unitOfWork.Accounts.Update(account);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var transaction = await unitOfWork.Transactions.GetByIdAsync(snapshot.TransactionId, cancellationToken);
        if (transaction is null)
            return;

        LogMemoryPersistence.RevertTransactionFromAccount(transaction.Account, transaction.Type, transaction.Amount);
        unitOfWork.Accounts.Update(transaction.Account);
        unitOfWork.Transactions.Remove(transaction);
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
        incomeLog.IsIoU = snapshot.IsIoU;
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
            IsIoU = snapshot.IsIoU
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
        var tag =
            await LogMemoryPersistence.GetRequiredTagAsync(unitOfWork, snapshot.TagId, cancellationToken);

        LogMemoryPersistence.RevertExpenseFromAccount(currentAccount, expenseLog.Amount);
        LogMemoryPersistence.ApplyExpenseToAccount(targetAccount, snapshot.Amount);

        expenseLog.Expense.Name = snapshot.ExpenseName;
        expenseLog.Expense.Amount = snapshot.Amount;
        expenseLog.Expense.ExpenseCategory = snapshot.ExpenseCategory;
        expenseLog.Expense.Account = targetAccount;
        expenseLog.Expense.Tag = tag;
        expenseLog.Expense.IsIoU = snapshot.IsIoU;

        expenseLog.Amount = snapshot.Amount;
        expenseLog.DeductedOn = snapshot.DeductedOn;
        expenseLog.Notes = snapshot.Notes;
        expenseLog.IsForDeletion = snapshot.IsForDeletion;
        expenseLog.IsIoU = snapshot.IsIoU;
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
            IsEnabled = snapshot.IsEnabled,
            IsDefault = snapshot.IsDefault
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
        account.IsDefault = snapshot.IsDefault;

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
            IsEnabled = snapshot.IsEnabled,
            IsDefault = snapshot.IsDefault
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
        expense.TagId = snapshot.TagId;
        expense.IsIoU = snapshot.IsIoU;

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
        var tag = await LogMemoryPersistence.GetRequiredTagAsync(unitOfWork, snapshot.TagId,
            cancellationToken);

        var expense = new Expense
        {
            Id = snapshot.ExpenseId,
            Name = snapshot.Name,
            Amount = snapshot.Amount,
            ExpenseCategory = snapshot.ExpenseCategory,
            AccountId = snapshot.AccountId,
            TagId = snapshot.TagId,
            Account = account,
            Tag = tag,
            IsIoU = snapshot.IsIoU
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

public sealed class DeleteTagMemoryAction(TagMemorySnapshot snapshot) : ILogMemoryAction
{
    public string Description => "Delete tag";

    public async Task UndoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        if (await unitOfWork.Tags.GetByIdAsync(snapshot.TagId, cancellationToken) is not null)
            return;

        var tag = new Tag
        {
            Id = snapshot.TagId,
            Name = snapshot.Name,
            HexCode = snapshot.HexCode,
            SpendingLimit = snapshot.SpendingLimit
        };

        await unitOfWork.Tags.AddAsync(tag, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RedoAsync(IUnitOfWork unitOfWork, CancellationToken cancellationToken = default)
    {
        var tag = await unitOfWork.Tags.GetByIdAsync(snapshot.TagId, cancellationToken);
        if (tag is null)
            return;

        unitOfWork.Tags.Remove(tag);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

public sealed class EditTagMemoryAction(
    TagMemorySnapshot before,
    TagMemorySnapshot after) : ILogMemoryAction
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

    private static async Task ApplySnapshotAsync(IUnitOfWork unitOfWork, TagMemorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var tag = await unitOfWork.Tags.GetByIdAsync(snapshot.TagId, cancellationToken);
        if (tag is null)
            return;

        tag.Name = snapshot.Name;
        tag.HexCode = snapshot.HexCode;
        tag.SpendingLimit = snapshot.SpendingLimit;
        unitOfWork.Tags.Update(tag);
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
    internal static void ApplyTransactionToAccount(Account account, TransactionType type, decimal amount)
    {
        if (type == TransactionType.Expense)
            ApplyExpenseToAccount(account, amount);
        else
            ApplyIncomeToAccount(account, amount);
    }

    internal static void RevertTransactionFromAccount(Account account, TransactionType type, decimal amount)
    {
        if (type == TransactionType.Expense)
            RevertExpenseFromAccount(account, amount);
        else
            RevertIncomeFromAccount(account, amount);
    }

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

    internal static async Task<Tag> GetRequiredTagAsync(IUnitOfWork unitOfWork, int tagId,
        CancellationToken cancellationToken)
    {
        var tag = await unitOfWork.Tags.GetByIdAsync(tagId, cancellationToken);
        return tag ?? throw new InvalidOperationException($"Unable to find expense tag {tagId}.");
    }
}
