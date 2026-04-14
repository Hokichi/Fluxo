using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services;

public sealed class SpendingSourceService(IUnitOfWork unitOfWork, IMapper mapper) : ISpendingSourceService
{
    public async Task<IReadOnlyList<SpendingSourceDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sources = await unitOfWork.SpendingSources.GetAllAsync(cancellationToken);
        return mapper.Map<IReadOnlyList<SpendingSourceDto>>(sources);
    }

    public async Task<IReadOnlyList<SpendingSourceDto>> SearchAsync(SpendingSourceFilter filter,
        CancellationToken cancellationToken = default)
    {
        var sources = await unitOfWork.SpendingSources.SearchAsync(filter, cancellationToken);
        return mapper.Map<IReadOnlyList<SpendingSourceDto>>(sources);
    }

    public async Task AddAsync(SpendingSourceDto dto, CancellationToken cancellationToken = default)
    {
        var source = mapper.Map<SpendingSource>(dto);
        source.Id = 0; // ensure EF treats this as a new insert
        await unitOfWork.SpendingSources.AddAsync(source, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var marked = await unitOfWork.SpendingSources.GetMarkedForDeletionAsync(cancellationToken);
        if (marked.Count == 0) return;

        var markedIds = marked.Select(s => s.Id).ToHashSet();

        // Load all expenses once to avoid a table scan per source.
        var allExpenses = await unitOfWork.Expenses.GetAllAsync(cancellationToken);

        foreach (var source in marked)
        {
            var sourceExpenseIds = allExpenses
                .Where(e => e.SpendingSourceId == source.Id)
                .Select(e => e.Id)
                .ToList();

            foreach (var expenseId in sourceExpenseIds)
            {
                var logs = await unitOfWork.ExpenseLogs.GetByExpenseIdAsync(expenseId, cancellationToken);

                // Restore balance on any OTHER spending source referenced by these logs.
                foreach (var grp in logs.GroupBy(l => l.SpendingSourceId).Where(g => !markedIds.Contains(g.Key)))
                {
                    var affectedSource = await unitOfWork.SpendingSources.GetByIdAsync(grp.Key, cancellationToken);
                    if (affectedSource is null) continue;
                    var total = grp.Sum(l => l.Amount);
                    affectedSource.Balance += total;
                    affectedSource.SpentAmount -= total;
                    unitOfWork.SpendingSources.Update(affectedSource);
                }

                foreach (var log in logs)
                    unitOfWork.ExpenseLogs.Remove(log);

                var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(expenseId, cancellationToken);
                if (expense is not null)
                    unitOfWork.Expenses.Remove(expense);
            }

            unitOfWork.SpendingSources.Remove(source);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task AddIncomeAsync(int spendingSourceId, decimal amount, string notes,
        CancellationToken cancellationToken = default)
    {
        var source = await unitOfWork.SpendingSources.GetByIdAsync(spendingSourceId, cancellationToken);
        if (source is null) return;

        source.Balance += amount;
        unitOfWork.SpendingSources.Update(source);

        var incomeLog = new IncomeLog
        {
            SpendingSourceId = spendingSourceId,
            Amount = amount,
            AddedOn = DateTime.Now,
            Notes = notes
        };
        await unitOfWork.IncomeLogs.AddAsync(incomeLog, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
