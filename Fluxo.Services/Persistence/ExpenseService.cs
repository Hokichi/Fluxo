using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class ExpenseService(IDataOperationRunner dataOperationRunner, IMapper mapper) : IExpenseService
{
    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync("load expenses", async (scope, ct) =>
        {
            var expenses = await scope.UnitOfWork.Expenses.GetAllAsync(ct);
            return mapper.Map<IReadOnlyList<ExpenseDto>>(expenses);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseDto>> SearchAsync(ExpenseFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync("search expenses", async (scope, ct) =>
        {
            var expenses = await scope.UnitOfWork.Expenses.SearchAsync(filter, ct);
            return mapper.Map<IReadOnlyList<ExpenseDto>>(expenses);
        }, cancellationToken);
    }

    public async Task AddAsync(ExpenseDto dto, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("log expense", async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;

            // Validate source exists before staging any entities.
            var source = await unitOfWork.SpendingSources.GetByIdAsync(dto.SpendingSourceId, ct);
            if (source is null)
                throw new InvalidOperationException($"SpendingSource with id {dto.SpendingSourceId} was not found.");

            // Build the entity manually so EF can track it and resolve the ExpenseLog FK.
            var expense = new Expense
            {
                SpendingSourceId = dto.SpendingSourceId,
                ExpenseTagId = dto.ExpenseTagId,
                Name = dto.Name,
                Amount = dto.Amount,
                ExpenseCategory = dto.ExpenseCategory
            };
            await unitOfWork.Expenses.AddAsync(expense, ct);

            // Link the log via navigation; EF resolves the FK after insert.
            var log = new ExpenseLog
            {
                Expense = expense,
                SpendingSourceId = dto.SpendingSourceId,
                Amount = dto.Amount,
                DeductedOn = DateTime.Now,
                Notes = string.Empty,
                IsForDeletion = false
            };
            await unitOfWork.ExpenseLogs.AddAsync(log, ct);

            ApplyExpenseToSpendingSource(source, dto.Amount);
            unitOfWork.SpendingSources.Update(source);

            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task UpdateAsync(ExpenseDto dto, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("update expense", async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(dto.Id, ct);
            if (expense is null)
                return;

            mapper.Map(dto, expense);
            unitOfWork.Expenses.Update(expense);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("remove expense", async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;

            // FK deletes are Restrict; remove logs before removing the expense.
            var logs = await unitOfWork.ExpenseLogs.GetByExpenseIdAsync(id, ct);

            // Restore balance on each affected spending source before deleting logs.
            foreach (var grp in logs.GroupBy(l => l.SpendingSourceId))
            {
                var source = await unitOfWork.SpendingSources.GetByIdAsync(grp.Key, ct);
                if (source is null)
                    continue;

                var total = grp.Sum(l => l.Amount);
                RestoreExpenseOnSpendingSource(source, total);
                unitOfWork.SpendingSources.Update(source);
            }

            foreach (var log in logs)
                unitOfWork.ExpenseLogs.Remove(log);

            var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(id, ct);
            if (expense is not null)
                unitOfWork.Expenses.Remove(expense);

            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    private static void ApplyExpenseToSpendingSource(SpendingSource source, decimal amount)
    {
        if (source.SpendingSourceType is SpendingSourceType.Credit or SpendingSourceType.BNPL)
        {
            source.SpentAmount += amount;
            return;
        }

        source.Balance -= amount;
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
