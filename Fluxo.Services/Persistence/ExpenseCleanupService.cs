using Fluxo.Core.Interfaces;

namespace Fluxo.Services.Persistence;

public sealed class ExpenseCleanupService(Func<IUnitOfWork> unitOfWorkFactory) : IExpenseCleanupService
{
    private readonly Func<IUnitOfWork> _unitOfWorkFactory = unitOfWorkFactory;

    public async Task DeleteMarkedExpenseLogsAsync(IEnumerable<int> expenseLogIdsMarkedForDeletion,
        CancellationToken cancellationToken = default)
    {
        var markedIds = expenseLogIdsMarkedForDeletion.ToHashSet();
        HashSet<int> expenseIdsToDelete;

        await using (var unitOfWork = _unitOfWorkFactory())
        {
            var expenseLogs = await unitOfWork.ExpenseLogs.GetAllAsync(cancellationToken);
            var expenseLogsToDelete = expenseLogs
                .Where(log => log.IsForDeletion || markedIds.Contains(log.Id))
                .ToList();

            if (expenseLogsToDelete.Count == 0)
                return;

            expenseIdsToDelete = expenseLogsToDelete
                .Select(log => log.Expense?.Id ?? 0)
                .Where(id => id > 0)
                .ToHashSet();

            foreach (var expenseLog in expenseLogsToDelete) unitOfWork.ExpenseLogs.Remove(expenseLog);

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (expenseIdsToDelete.Count == 0)
            return;

        await using var cleanupUnitOfWork = _unitOfWorkFactory();

        var remainingExpenseLogExpenseIds = (await cleanupUnitOfWork.ExpenseLogs.GetAllAsync(cancellationToken))
            .Select(log => log.Expense?.Id ?? 0)
            .Where(id => id > 0)
            .ToHashSet();

        expenseIdsToDelete.ExceptWith(remainingExpenseLogExpenseIds);
        if (expenseIdsToDelete.Count == 0)
            return;

        var expenses = await cleanupUnitOfWork.Expenses.GetAllAsync(cancellationToken);
        foreach (var expense in expenses.Where(expense => expenseIdsToDelete.Contains(expense.Id)))
            cleanupUnitOfWork.Expenses.Remove(expense);

        await cleanupUnitOfWork.SaveChangesAsync(cancellationToken);
    }
}