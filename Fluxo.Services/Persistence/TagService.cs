using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class TagService(IDataOperationRunner dataOperationRunner, IMapper mapper) : ITagService
{
    public async Task<IReadOnlyList<ExpenseTagDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync("load expense tags", async (scope, ct) =>
        {
            var tags = await scope.UnitOfWork.ExpenseTags.GetAllAsync(ct);
            return mapper.Map<IReadOnlyList<ExpenseTagDto>>(tags);
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseTagDto>> GetTagsOrderedByExpenseCountAsync(ExpenseFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync("load tags ordered by expense count", async (scope, ct) =>
        {
            // Repository aggregation methods do not accept ExpenseFilter, so we group in memory.
            var expenses = await scope.UnitOfWork.Expenses.SearchAsync(filter, ct);

            var tags = expenses
                .Where(e => e.ExpenseTag is not null)
                .GroupBy(e => e.ExpenseTagId)
                .OrderByDescending(g => g.Count())
                .Select(g => g.First().ExpenseTag!)
                .ToList();

            return mapper.Map<IReadOnlyList<ExpenseTagDto>>(tags);
        }, cancellationToken);
    }

    public async Task AddAsync(ExpenseTagDto dto, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("create tag", async (scope, ct) =>
        {
            var tag = mapper.Map<ExpenseTag>(dto);
            tag.Id = 0;
            await scope.UnitOfWork.ExpenseTags.AddAsync(tag, ct);
            await scope.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task UpdateAsync(ExpenseTagDto dto, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("update tag", async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var tag = await unitOfWork.ExpenseTags.GetByIdAsync(dto.Id, ct);
            if (tag is null)
                return;

            mapper.Map(dto, tag);
            unitOfWork.ExpenseTags.Update(tag);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("delete tag", async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var tag = await unitOfWork.ExpenseTags.GetByIdAsync(id, ct);
            if (tag is null)
                return;

            // If Expense rows still reference this tag, FK (Restrict) will reject delete.
            unitOfWork.ExpenseTags.Remove(tag);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
