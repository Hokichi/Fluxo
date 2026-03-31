using System;
using System.Threading;
using System.Threading.Tasks;
using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.Core.Interfaces;

public interface IViewModelWriteUnitOfWork<TExpenseViewModel, TExpenseLogViewModel, TExpenseTagViewModel, TSavingGoalViewModel, TSpendingSourceViewModel>
    : IDisposable, IAsyncDisposable
    where TExpenseViewModel : class
    where TExpenseLogViewModel : class
    where TExpenseTagViewModel : class
    where TSavingGoalViewModel : class
    where TSpendingSourceViewModel : class
{
    IWriteRepository<TExpenseViewModel> Expenses { get; }
    IWriteRepository<TExpenseLogViewModel> ExpenseLogs { get; }
    IWriteRepository<TExpenseTagViewModel> ExpenseTags { get; }
    IWriteRepository<TSavingGoalViewModel> SavingGoals { get; }
    IWriteRepository<TSpendingSourceViewModel> SpendingSources { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
