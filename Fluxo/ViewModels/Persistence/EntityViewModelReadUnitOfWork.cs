using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.ViewModels.Entities;

namespace Fluxo.ViewModels.Persistence;

public sealed class EntityViewModelReadUnitOfWork(
    IUnitOfWork unitOfWork,
    IExpenseReadRepository<ExpenseVM> expenses,
    IExpenseLogReadRepository<ExpenseLogVM> expenseLogs,
    IIncomeLogReadRepository<IncomeLogVM> incomeLogs,
    IExpenseTagReadRepository<ExpenseTagVM> expenseTags,
    IReadRepository<SavingGoalVM> savingGoals,
    ISpendingSourceReadRepository<SpendingSourceVM> spendingSources)
    : IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public IExpenseReadRepository<ExpenseVM> Expenses { get; } = expenses;
    public IExpenseLogReadRepository<ExpenseLogVM> ExpenseLogs { get; } = expenseLogs;
    public IIncomeLogReadRepository<IncomeLogVM> IncomeLogs { get; } = incomeLogs;
    public IExpenseTagReadRepository<ExpenseTagVM> ExpenseTags { get; } = expenseTags;
    public IReadRepository<SavingGoalVM> SavingGoals { get; } = savingGoals;
    public ISpendingSourceReadRepository<SpendingSourceVM> SpendingSources { get; } = spendingSources;

    public void Dispose()
    {
        _unitOfWork.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        return _unitOfWork.DisposeAsync();
    }
}