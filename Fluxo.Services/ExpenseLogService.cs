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
        var expenseIds = markedLogs.Select(l => l.ExpenseId).Distinct().ToList();

        foreach (var log in markedLogs)
            unitOfWork.ExpenseLogs.Remove(log);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // After deleting logs, remove any expenses that have no remaining logs.
        foreach (var expenseId in expenseIds)
        {
            var remaining = await unitOfWork.ExpenseLogs.GetByExpenseIdAsync(expenseId, cancellationToken);
            if (remaining.Count > 0) continue;

            var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(expenseId, cancellationToken);
            if (expense is not null)
                unitOfWork.Expenses.Remove(expense);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
