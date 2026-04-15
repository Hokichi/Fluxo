using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class ExpenseService(IUnitOfWork unitOfWork, IMapper mapper) : IExpenseService
{
    public async Task<IReadOnlyList<ExpenseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var expenses = await unitOfWork.Expenses.GetAllAsync(cancellationToken);
        return mapper.Map<IReadOnlyList<ExpenseDto>>(expenses);
    }

    public async Task<IReadOnlyList<ExpenseDto>> SearchAsync(ExpenseFilter filter,
        CancellationToken cancellationToken = default)
    {
        var expenses = await unitOfWork.Expenses.SearchAsync(filter, cancellationToken);
        return mapper.Map<IReadOnlyList<ExpenseDto>>(expenses);
    }

    public async Task AddAsync(ExpenseDto dto, CancellationToken cancellationToken = default)
    {
        // Validate source exists before staging any entities.
        var source = await unitOfWork.SpendingSources.GetByIdAsync(dto.SpendingSourceId, cancellationToken);
        if (source is null)
            throw new InvalidOperationException($"SpendingSource with id {dto.SpendingSourceId} was not found.");

        // Build the entity manually so EF can track it and resolve the ExpenseLog FK.
        var expense = new Expense
        {
            SpendingSourceId = dto.SpendingSourceId,
            ExpenseTagId = dto.ExpenseTagId,
            Name = dto.Name,
            Amount = dto.Amount,
            ExpenseKind = dto.ExpenseKind,
            ExpenseCategory = dto.ExpenseCategory,
            RecurringDate = dto.RecurringDate,
            IsActive = dto.IsActive
        };
        await unitOfWork.Expenses.AddAsync(expense, cancellationToken);

        // Link the log via navigation — EF resolves the FK after insert.
        var log = new ExpenseLog
        {
            Expense = expense,
            SpendingSourceId = dto.SpendingSourceId,
            Amount = dto.Amount,
            DeductedOn = DateTime.Now,
            Notes = string.Empty,
            IsForDeletion = false
        };
        await unitOfWork.ExpenseLogs.AddAsync(log, cancellationToken);

        // Deduct from spending source.
        source.Balance -= dto.Amount;
        source.SpentAmount += dto.Amount;
        unitOfWork.SpendingSources.Update(source);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ExpenseDto dto, CancellationToken cancellationToken = default)
    {
        var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(dto.Id, cancellationToken);
        if (expense is null) return;

        mapper.Map(dto, expense);
        unitOfWork.Expenses.Update(expense);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        // FK deletes are Restrict — remove logs before removing the expense.
        var logs = await unitOfWork.ExpenseLogs.GetByExpenseIdAsync(id, cancellationToken);

        // Restore balance on each affected spending source before deleting logs.
        foreach (var grp in logs.GroupBy(l => l.SpendingSourceId))
        {
            var source = await unitOfWork.SpendingSources.GetByIdAsync(grp.Key, cancellationToken);
            if (source is null) continue;
            var total = grp.Sum(l => l.Amount);
            source.Balance += total;
            source.SpentAmount -= total;
            unitOfWork.SpendingSources.Update(source);
        }

        foreach (var log in logs)
            unitOfWork.ExpenseLogs.Remove(log);

        var expense = await unitOfWork.Expenses.GetByExpenseIdAsync(id, cancellationToken);
        if (expense is not null)
            unitOfWork.Expenses.Remove(expense);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
