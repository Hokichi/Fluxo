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
        return await dataOperationRunner.RunAsync("load expense logs", async (scope, ct) =>
        {
            var logs = await scope.UnitOfWork.ExpenseLogs.GetAllAsync(ct);
            return mapper.Map<IReadOnlyList<ExpenseLogDto>>(logs);
        }, cancellationToken);
    }

    public async Task<ExpenseLogDto?> GetByLogIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync("load expense log", async (scope, ct) =>
        {
            var log = await scope.UnitOfWork.ExpenseLogs.GetByLogIdAsync(id, ct);
            return log is null ? null : mapper.Map<ExpenseLogDto>(log);
        }, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("delete expense log", async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var log = await unitOfWork.ExpenseLogs.GetByLogIdAsync(id, ct);
            if (log is null || log.IsForDeletion)
                return;

            var source = await unitOfWork.Accounts.GetByIdAsync(log.AccountId, ct);
            if (source is not null)
            {
                RestoreExpenseOnAccount(source, log.Amount);
                unitOfWork.Accounts.Update(source);
            }

            log.IsForDeletion = true;
            unitOfWork.ExpenseLogs.Update(log);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task PostTerminationCleanupAsync(CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("cleanup terminated expense logs", async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var markedIncomeLogs = await unitOfWork.IncomeLogs.GetMarkedForDeletionAsync(ct);
            foreach (var incomeLog in markedIncomeLogs)
                unitOfWork.IncomeLogs.Remove(incomeLog);

            var markedLogs = await unitOfWork.ExpenseLogs.GetMarkedForDeletionAsync(ct);
            if (markedLogs.Count == 0)
            {
                if (markedIncomeLogs.Count > 0)
                    await unitOfWork.SaveChangesAsync(ct);

                return;
            }

            var expenseIds = markedLogs.Select(l => l.ExpenseId).Distinct().ToList();

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

    private static void RestoreExpenseOnAccount(Account source, decimal amount)
    {
        if (source.AccountType is AccountType.Credit or AccountType.BNPL)
        {
            source.SpentAmount = Math.Max(0m, source.SpentAmount - amount);
            return;
        }

        source.Balance += amount;
    }
}
