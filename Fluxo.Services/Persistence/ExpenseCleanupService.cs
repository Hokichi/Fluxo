using Fluxo.Core.Interfaces;

namespace Fluxo.Services.Persistence;

public sealed class ExpenseCleanupService(IUnitOfWork unitOfWork) : IExpenseCleanupService
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task DeleteMarkedExpenseLogsAsync(IEnumerable<int> expenseLogIdsMarkedForDeletion,
        CancellationToken cancellationToken = default)
    {
        var markedIds = expenseLogIdsMarkedForDeletion.ToHashSet();

        var unitOfWork = _unitOfWork;

        var expenseLogs = await unitOfWork.ExpenseLogs.GetAllAsync(cancellationToken);
        var expenseLogsToDelete = expenseLogs
            .Where(log => log.IsForDeletion || markedIds.Contains(log.Id))
            .ToList();

        if (expenseLogsToDelete.Count == 0)
            return;

        var expenseIdsToDelete = expenseLogsToDelete
            .Select(log => log.Expense?.Id ?? 0)
            .Where(id => id > 0)
            .ToHashSet();

        foreach (var expenseLog in expenseLogsToDelete) unitOfWork.ExpenseLogs.Remove(expenseLog);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (expenseIdsToDelete.Count == 0)
            return;

        var remainingExpenseLogExpenseIds = (await unitOfWork.ExpenseLogs.GetAllAsync(cancellationToken))
            .Select(log => log.Expense?.Id ?? 0)
            .Where(id => id > 0)
            .ToHashSet();

        expenseIdsToDelete.ExceptWith(remainingExpenseLogExpenseIds);
        if (expenseIdsToDelete.Count == 0)
            return;

        var expenses = await unitOfWork.Expenses.GetAllAsync(cancellationToken);
        foreach (var expense in expenses.Where(expense => expenseIdsToDelete.Contains(expense.Id)))
            unitOfWork.Expenses.Remove(expense);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
