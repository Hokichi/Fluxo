using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Persistence;

public sealed class EntityViewModelWriteUnitOfWork(
    IUnitOfWork unitOfWork,
    IWriteRepository<ExpenseVM> expenses,
    IWriteRepository<ExpenseLogVM> expenseLogs,
    IWriteRepository<IncomeLogVM> incomeLogs,
    IWriteRepository<ExpenseTagVM> expenseTags,
    IWriteRepository<SavingGoalVM> savingGoals,
    IWriteRepository<SpendingSourceVM> spendingSources)
    : IViewModelWriteUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public IWriteRepository<ExpenseVM> Expenses { get; } = expenses;
    public IWriteRepository<ExpenseLogVM> ExpenseLogs { get; } = expenseLogs;
    public IWriteRepository<IncomeLogVM> IncomeLogs { get; } = incomeLogs;
    public IWriteRepository<ExpenseTagVM> ExpenseTags { get; } = expenseTags;
    public IWriteRepository<SavingGoalVM> SavingGoals { get; } = savingGoals;
    public IWriteRepository<SpendingSourceVM> SpendingSources { get; } = spendingSources;

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _unitOfWork.DisposeAsync();
    }
}