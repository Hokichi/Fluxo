using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class ExpenseLogService(IDataOperationRunner dataOperationRunner, IMapper mapper) : IExpenseLogService
{
    public async Task<IReadOnlyList<ExpenseLogDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var logs = await scope.UnitOfWork.ExpenseLogs.GetAllAsync(ct);
            return mapper.Map<IReadOnlyList<ExpenseLogDto>>(logs);
        }, cancellationToken);
    }

    public async Task<ExpenseLogDto?> GetByLogIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var log = await scope.UnitOfWork.ExpenseLogs.GetByLogIdAsync(id, ct);
            return log is null ? null : mapper.Map<ExpenseLogDto>(log);
        }, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var log = await unitOfWork.ExpenseLogs.GetByLogIdAsync(id, ct);
            if (log is null || log.IsForDeletion)
                return;

            var source = await unitOfWork.SpendingSources.GetByIdAsync(log.SpendingSourceId, ct);
            if (source is not null)
            {
                RestoreExpenseOnSpendingSource(source, log.Amount);
                unitOfWork.SpendingSources.Update(source);
            }

            log.IsForDeletion = true;
            unitOfWork.ExpenseLogs.Update(log);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task PostTerminationCleanupAsync(CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var markedLogs = await unitOfWork.ExpenseLogs.GetMarkedForDeletionAsync(ct);
            if (markedLogs.Count == 0)
                return;

            var expenseIds = markedLogs.Select(l => l.ExpenseId).Distinct().ToList();

            // Restore balance on spending sources for logs being permanently deleted.
            foreach (var grp in markedLogs.GroupBy(l => l.SpendingSourceId))
            {
                var source = await unitOfWork.SpendingSources.GetByIdAsync(grp.Key, ct);
                if (source is null)
                    continue;

                var total = grp.Sum(l => l.Amount);
                RestoreExpenseOnSpendingSource(source, total);
                unitOfWork.SpendingSources.Update(source);
            }

            foreach (var log in markedLogs)
                unitOfWork.ExpenseLogs.Remove(log);

            await unitOfWork.SaveChangesAsync(ct);

            // After committing log deletions, check which expenses are now orphaned.
            // Load all remaining logs once to avoid an N+1 query per expense ID.
            var remainingLogs = await unitOfWork.ExpenseLogs.GetAllAsync(ct);
            var expensesWithLogs = remainingLogs.Select(l => l.ExpenseId).ToHashSet();

            foreach (var expenseId in expenseIds.Where(candidateId => !expensesWithLogs.Contains(candidateId)))
            {
                var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(expenseId, ct);
                if (expense is not null)
                    unitOfWork.Expenses.Remove(expense);
            }

            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    private static void RestoreExpenseOnSpendingSource(SpendingSource source, decimal amount)
    {
        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            source.SpentAmount = Math.Max(0m, source.SpentAmount - amount);
            return;
        }

        source.Balance += amount;
    }
}
