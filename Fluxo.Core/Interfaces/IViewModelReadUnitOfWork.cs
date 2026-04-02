using System;
using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.Core.Interfaces;

public interface IViewModelReadUnitOfWork<TExpenseViewModel, TExpenseLogViewModel, TIncomeLogViewModel, TExpenseTagViewModel, TSavingGoalViewModel, TSpendingSourceViewModel>
    : IDisposable, IAsyncDisposable
    where TExpenseViewModel : class
    where TExpenseLogViewModel : class
    where TIncomeLogViewModel : class
    where TExpenseTagViewModel : class
    where TSavingGoalViewModel : class
    where TSpendingSourceViewModel : class
{
    IExpenseReadRepository<TExpenseViewModel> Expenses { get; }
    IExpenseLogReadRepository<TExpenseLogViewModel> ExpenseLogs { get; }
    IIncomeLogReadRepository<TIncomeLogViewModel> IncomeLogs { get; }
    IExpenseTagReadRepository<TExpenseTagViewModel> ExpenseTags { get; }
    IReadRepository<TSavingGoalViewModel> SavingGoals { get; }
    ISpendingSourceReadRepository<TSpendingSourceViewModel> SpendingSources { get; }
}
