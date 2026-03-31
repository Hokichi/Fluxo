using System;
using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.Core.Interfaces;

public interface IViewModelReadUnitOfWork<TExpenseViewModel, TExpenseLogViewModel, TExpenseTagViewModel, TSavingGoalViewModel, TSpendingSourceViewModel>
    : IDisposable, IAsyncDisposable
    where TExpenseViewModel : class
    where TExpenseLogViewModel : class
    where TExpenseTagViewModel : class
    where TSavingGoalViewModel : class
    where TSpendingSourceViewModel : class
{
    IReadRepository<TExpenseViewModel> Expenses { get; }
    IReadRepository<TExpenseLogViewModel> ExpenseLogs { get; }
    IReadRepository<TExpenseTagViewModel> ExpenseTags { get; }
    IReadRepository<TSavingGoalViewModel> SavingGoals { get; }
    IReadRepository<TSpendingSourceViewModel> SpendingSources { get; }
}
