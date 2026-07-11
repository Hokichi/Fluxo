using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class AppDataService(IDataOperationRunner runner) : IAppDataService
{
    private readonly Lock _pendingLock = new();
    private readonly List<Func<IUnitOfWork, CancellationToken, Task>> _pending = [];
    private readonly SemaphoreSlim _saveGate = new(1, 1);
    private readonly SemaphoreSlim _allocationGate = new(1, 1);
    private BudgetAllocation? _pendingAllocation;
    private IUnitOfWork? _directUnitOfWork;

    public AppDataService(IUnitOfWork unitOfWork) : this(new DirectOperationRunner(unitOfWork))
    {
        _directUnitOfWork = unitOfWork;
    }

    public Task<IReadOnlyList<Transaction>> GetTransactionsAsync(CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.Transactions.GetAllAsync(ct), cancellationToken);

    public Task<Transaction?> GetTransactionByIdAsync(int id, CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.Transactions.GetByIdAsync(id, ct), cancellationToken);

    public Task AddTransactionAsync(Transaction entity, CancellationToken cancellationToken = default) =>
        Enqueue((unitOfWork, ct) => unitOfWork.Transactions.AddAsync(entity, ct), cancellationToken);

    public void UpdateTransaction(Transaction entity) =>
        Enqueue(unitOfWork => unitOfWork.Transactions.Update(entity));

    public void RemoveTransaction(Transaction entity) =>
        Enqueue(unitOfWork => unitOfWork.Transactions.Remove(entity));

    public Task<IReadOnlyList<Tag>> GetTagsAsync(CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.Tags.GetAllAsync(ct), cancellationToken);

    public Task<Tag?> GetTagByIdAsync(int id, CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.Tags.GetByIdAsync(id, ct), cancellationToken);

    public Task<IReadOnlyList<(Tag Tag, int Count)>> GetTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.Tags.GetTagsByCountDescendingAsync(ct), cancellationToken);

    public Task AddTagAsync(Tag entity, CancellationToken cancellationToken = default) =>
        Enqueue((unitOfWork, ct) => unitOfWork.Tags.AddAsync(entity, ct), cancellationToken);

    public void UpdateTag(Tag entity) => Enqueue(unitOfWork => unitOfWork.Tags.Update(entity));
    public void RemoveTag(Tag entity) => Enqueue(unitOfWork => unitOfWork.Tags.Remove(entity));

    public Task<IReadOnlyList<SavingGoal>> GetSavingGoalsAsync(CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.SavingGoals.GetAllAsync(ct), cancellationToken);

    public Task<SavingGoal?> GetSavingGoalByIdAsync(int id, CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.SavingGoals.GetByIdAsync(id, ct), cancellationToken);

    public Task AddSavingGoalAsync(SavingGoal entity, CancellationToken cancellationToken = default) =>
        Enqueue((unitOfWork, ct) => unitOfWork.SavingGoals.AddAsync(entity, ct), cancellationToken);

    public void UpdateSavingGoal(SavingGoal entity) =>
        Enqueue(unitOfWork => unitOfWork.SavingGoals.Update(entity));

    public void RemoveSavingGoal(SavingGoal entity) =>
        Enqueue(unitOfWork => unitOfWork.SavingGoals.Remove(entity));

    public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.Accounts.GetAllAsync(ct), cancellationToken);

    public Task<Account?> GetAccountByIdAsync(int id, CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.Accounts.GetByIdAsync(id, ct), cancellationToken);

    public Task AddAccountAsync(Account entity, CancellationToken cancellationToken = default) =>
        Enqueue((unitOfWork, ct) => unitOfWork.Accounts.AddAsync(entity, ct), cancellationToken);

    public void UpdateAccount(Account entity) => Enqueue(unitOfWork => unitOfWork.Accounts.Update(entity));
    public void RemoveAccount(Account entity) => Enqueue(unitOfWork => unitOfWork.Accounts.Remove(entity));

    public Task<IReadOnlyList<RecurringTransaction>> GetRecurringTransactionsAsync(
        CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.RecurringTransactions.GetAllAsync(ct), cancellationToken);

    public Task<RecurringTransaction?> GetRecurringTransactionByIdAsync(
        int id,
        CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.RecurringTransactions.GetByIdAsync(id, ct), cancellationToken);

    public Task AddRecurringTransactionAsync(
        RecurringTransaction entity,
        CancellationToken cancellationToken = default) =>
        Enqueue((unitOfWork, ct) => unitOfWork.RecurringTransactions.AddAsync(entity, ct), cancellationToken);

    public void UpdateRecurringTransaction(RecurringTransaction entity) =>
        Enqueue(unitOfWork => unitOfWork.RecurringTransactions.Update(entity));

    public void RemoveRecurringTransaction(RecurringTransaction entity) =>
        Enqueue(unitOfWork => unitOfWork.RecurringTransactions.Remove(entity));

    public Task<IReadOnlyList<UserSettings>> GetUserSettingsAsync(CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.UserSettings.GetAllAsync(ct), cancellationToken);

    public Task<UserSettings?> GetUserSettingByNameAsync(
        string name,
        CancellationToken cancellationToken = default) =>
        runner.RunAsync((scope, ct) => scope.UnitOfWork.UserSettings.GetByNameAsync(name, ct), cancellationToken);

    public Task AddUserSettingAsync(UserSettings entity, CancellationToken cancellationToken = default) =>
        Enqueue((unitOfWork, ct) => unitOfWork.UserSettings.AddAsync(entity, ct), cancellationToken);

    public void UpdateUserSetting(UserSettings entity) =>
        Enqueue(unitOfWork => unitOfWork.UserSettings.Update(entity));

    public void RemoveUserSetting(UserSettings entity) =>
        Enqueue(unitOfWork => unitOfWork.UserSettings.Remove(entity));

    public Task<BudgetAllocation> GetBudgetAllocationAsync(CancellationToken cancellationToken = default) =>
        EnsureBudgetAllocationAsync(cancellationToken);

    public async Task<BudgetAllocation> EnsureBudgetAllocationAsync(CancellationToken cancellationToken = default)
    {
        await _allocationGate.WaitAsync(cancellationToken);
        try
        {
            if (_pendingAllocation is not null)
                return _pendingAllocation;

            var existing = await runner.RunAsync(
                (scope, ct) => scope.UnitOfWork.BudgetAllocation.GetAsync(ct), cancellationToken);
            if (existing is not null)
                return existing;

            var allocation = new BudgetAllocation();
            _pendingAllocation = allocation;
            await Enqueue((unitOfWork, ct) => unitOfWork.BudgetAllocation.AddAsync(allocation, ct), cancellationToken);
            return allocation;
        }
        finally
        {
            _allocationGate.Release();
        }
    }

    public void UpdateBudgetAllocation(BudgetAllocation entity) =>
        Enqueue(unitOfWork => unitOfWork.BudgetAllocation.Update(entity));

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_directUnitOfWork is not null)
        {
            await _directUnitOfWork.SaveChangesAsync(cancellationToken);
            _pendingAllocation = null;
            return;
        }

        await _saveGate.WaitAsync(cancellationToken);
        try
        {
            Func<IUnitOfWork, CancellationToken, Task>[] operations;
            lock (_pendingLock)
                operations = [.. _pending];

            await runner.RunInTransactionAsync("save data changes", async (scope, ct) =>
            {
                foreach (var operation in operations)
                    await operation(scope.UnitOfWork, ct);

                await scope.UnitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);

            lock (_pendingLock)
            {
                _pending.RemoveRange(0, operations.Length);
                _pendingAllocation = null;
            }
        }
        finally
        {
            _saveGate.Release();
        }
    }

    private Task Enqueue(
        Func<IUnitOfWork, CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_directUnitOfWork is not null)
            return operation(_directUnitOfWork, cancellationToken);

        lock (_pendingLock)
            _pending.Add(operation);
        return Task.CompletedTask;
    }

    private void Enqueue(Action<IUnitOfWork> operation)
    {
        if (_directUnitOfWork is not null)
        {
            operation(_directUnitOfWork);
            return;
        }

        lock (_pendingLock)
        {
            _pending.Add((unitOfWork, _) =>
            {
                operation(unitOfWork);
                return Task.CompletedTask;
            });
        }
    }

    private sealed class DirectOperationRunner(IUnitOfWork unitOfWork) : IDataOperationRunner
    {
        private readonly IDataOperationScope _scope = new DirectOperationScope(unitOfWork);

        public Task RunAsync(Func<IDataOperationScope, CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) => operation(_scope, cancellationToken);

        public Task RunAsync(string performedProcess,
            Func<IDataOperationScope, CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) => operation(_scope, cancellationToken);

        public Task<TResult> RunAsync<TResult>(
            Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) => operation(_scope, cancellationToken);

        public Task<TResult> RunAsync<TResult>(string performedProcess,
            Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) => operation(_scope, cancellationToken);

        public Task RunInTransactionAsync(string performedProcess,
            Func<IDataOperationScope, CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) => operation(_scope, cancellationToken);

        public Task<TResult> RunInTransactionAsync<TResult>(string performedProcess,
            Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default) => operation(_scope, cancellationToken);
    }

    private sealed class DirectOperationScope(IUnitOfWork unitOfWork) : IDataOperationScope
    {
        public IServiceProvider ServiceProvider { get; } = new UnitOfWorkServiceProvider(unitOfWork);
        public IUnitOfWork UnitOfWork { get; } = unitOfWork;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class UnitOfWorkServiceProvider(IUnitOfWork unitOfWork) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IUnitOfWork) ? unitOfWork : null;
    }
}
