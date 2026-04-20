using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class SpendingSourceService(IDataOperationRunner dataOperationRunner, IMapper mapper) : ISpendingSourceService
{
    public async Task<IReadOnlyList<SpendingSourceDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var sources = await scope.UnitOfWork.SpendingSources.GetAllAsync(ct);
            return mapper.Map<IReadOnlyList<SpendingSourceDto>>(sources);
        }, cancellationToken);
    }

    public async Task<SpendingSourceDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var source = await scope.UnitOfWork.SpendingSources.GetByIdAsync(id, ct);
            return source is null ? null : mapper.Map<SpendingSourceDto>(source);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<SpendingSourceDto>> SearchAsync(SpendingSourceFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var sources = await scope.UnitOfWork.SpendingSources.SearchAsync(filter, ct);
            return mapper.Map<IReadOnlyList<SpendingSourceDto>>(sources);
        }, cancellationToken);
    }

    public async Task AddAsync(SpendingSourceDto dto, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var source = mapper.Map<SpendingSource>(dto);
            source.Id = 0; // ensure EF treats this as a new insert
            await scope.UnitOfWork.SpendingSources.AddAsync(source, ct);
            await scope.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var marked = await unitOfWork.SpendingSources.GetMarkedForDeletionAsync(ct);
            if (marked.Count == 0)
                return;

            var markedIds = marked.Select(s => s.Id).ToHashSet();

            // Load all expenses once to avoid a table scan per source.
            var allExpenses = await unitOfWork.Expenses.GetAllAsync(ct);

            foreach (var source in marked)
            {
                var sourceExpenseIds = allExpenses
                    .Where(e => e.SpendingSourceId == source.Id)
                    .Select(e => e.Id)
                    .ToList();

                foreach (var expenseId in sourceExpenseIds)
                {
                    var logs = await unitOfWork.ExpenseLogs.GetByExpenseIdAsync(expenseId, ct);

                    // Restore balance on any OTHER spending source referenced by these logs.
                    foreach (var grp in logs.GroupBy(l => l.SpendingSourceId).Where(g => !markedIds.Contains(g.Key)))
                    {
                        var affectedSource = await unitOfWork.SpendingSources.GetByIdAsync(grp.Key, ct);
                        if (affectedSource is null)
                            continue;

                        var total = grp.Sum(l => l.Amount);
                        affectedSource.Balance += total;
                        affectedSource.SpentAmount -= total;
                        unitOfWork.SpendingSources.Update(affectedSource);
                    }

                    foreach (var log in logs)
                        unitOfWork.ExpenseLogs.Remove(log);

                    var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(expenseId, ct);
                    if (expense is not null)
                        unitOfWork.Expenses.Remove(expense);
                }

                unitOfWork.SpendingSources.Remove(source);
            }

            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task AddIncomeAsync(int spendingSourceId, decimal amount, string notes,
        CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var source = await unitOfWork.SpendingSources.GetByIdAsync(spendingSourceId, ct);
            if (source is null)
                return;

            source.Balance += amount;
            unitOfWork.SpendingSources.Update(source);

            var incomeLog = new IncomeLog
            {
                SpendingSourceId = spendingSourceId,
                Amount = amount,
                AddedOn = DateTime.Now,
                Notes = notes
            };
            await unitOfWork.IncomeLogs.AddAsync(incomeLog, ct);

            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
