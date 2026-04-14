using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services;

public sealed class ExpenseLogService(IUnitOfWork unitOfWork, IMapper mapper) : IExpenseLogService
{
    public async Task<IReadOnlyList<ExpenseLogDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var logs = await unitOfWork.ExpenseLogs.GetAllAsync(cancellationToken);
        return mapper.Map<IReadOnlyList<ExpenseLogDto>>(logs);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var log = await unitOfWork.ExpenseLogs.GetByLogIdAsync(id, cancellationToken);
        if (log is null) return;

        log.IsForDeletion = true;
        unitOfWork.ExpenseLogs.Update(log);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task PostTerminationCleanupAsync(CancellationToken cancellationToken = default)
    {
        var markedLogs = await unitOfWork.ExpenseLogs.GetMarkedForDeletionAsync(cancellationToken);
        if (markedLogs.Count == 0) return;

        var expenseIds = markedLogs.Select(l => l.ExpenseId).Distinct().ToList();

        // Restore balance on spending sources for logs being permanently deleted.
        foreach (var grp in markedLogs.GroupBy(l => l.SpendingSourceId))
        {
            var source = await unitOfWork.SpendingSources.GetByIdAsync(grp.Key, cancellationToken);
            if (source is null) continue;
            var total = grp.Sum(l => l.Amount);
            source.Balance += total;
            source.SpentAmount -= total;
            unitOfWork.SpendingSources.Update(source);
        }

        foreach (var log in markedLogs)
            unitOfWork.ExpenseLogs.Remove(log);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // After committing log deletions, check which expenses are now orphaned.
        // Load all remaining logs once to avoid an N+1 query per expense ID.
        var remainingLogs = await unitOfWork.ExpenseLogs.GetAllAsync(cancellationToken);
        var expensesWithLogs = remainingLogs.Select(l => l.ExpenseId).ToHashSet();

        foreach (var expenseId in expenseIds.Where(id => !expensesWithLogs.Contains(id)))
        {
            var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(expenseId, cancellationToken);
            if (expense is not null)
                unitOfWork.Expenses.Remove(expense);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
