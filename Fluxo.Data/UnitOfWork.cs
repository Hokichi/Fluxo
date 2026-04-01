using System.Threading;
using System.Threading.Tasks;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Fluxo.Data.Repositories;

namespace Fluxo.Data;

public sealed class UnitOfWork(FluxoDbContext dbContext) : IUnitOfWork
{
    private readonly FluxoDbContext _dbContext = dbContext;
    private IExpenseRepository? _expenses;
    private IExpenseLogRepository? _expenseLogs;
    private IIncomeLogRepository? _incomeLogs;
    private IExpenseTagRepository? _expenseTags;
    private ISavingGoalRepository? _savingGoals;
    private ISpendingSourceRepository? _spendingSources;

    public IExpenseRepository Expenses => _expenses ??= new ExpenseRepository(_dbContext);
    public IExpenseLogRepository ExpenseLogs => _expenseLogs ??= new ExpenseLogRepository(_dbContext);
    public IIncomeLogRepository IncomeLogs => _incomeLogs ??= new IncomeLogRepository(_dbContext);
    public IExpenseTagRepository ExpenseTags => _expenseTags ??= new ExpenseTagRepository(_dbContext);
    public ISavingGoalRepository SavingGoals => _savingGoals ??= new SavingGoalRepository(_dbContext);
    public ISpendingSourceRepository SpendingSources => _spendingSources ??= new SpendingSourceRepository(_dbContext);

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
