using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services;

public sealed class TagService(IUnitOfWork unitOfWork, IMapper mapper) : ITagService
{
    public async Task<IReadOnlyList<ExpenseTagDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tags = await unitOfWork.ExpenseTags.GetAllAsync(cancellationToken);
        return mapper.Map<IReadOnlyList<ExpenseTagDto>>(tags);
    }

    public async Task<IReadOnlyList<ExpenseTagDto>> GetTagsOrderedByExpenseCountAsync(ExpenseFilter filter,
        CancellationToken cancellationToken = default)
    {
        // SearchAsync includes ExpenseTag navigations — group in memory after fetch.
        var expenses = await unitOfWork.Expenses.SearchAsync(filter, cancellationToken);

        var tags = expenses
            .Where(e => e.ExpenseTag is not null)
            .GroupBy(e => e.ExpenseTagId)
            .OrderByDescending(g => g.Count())
            .Select(g => g.First().ExpenseTag!)
            .ToList();

        return mapper.Map<IReadOnlyList<ExpenseTagDto>>(tags);
    }

    public async Task AddAsync(ExpenseTagDto dto, CancellationToken cancellationToken = default)
    {
        var tag = mapper.Map<ExpenseTag>(dto);
        tag.Id = 0;
        await unitOfWork.ExpenseTags.AddAsync(tag, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ExpenseTagDto dto, CancellationToken cancellationToken = default)
    {
        var tag = await unitOfWork.ExpenseTags.GetByIdAsync(dto.Id, cancellationToken);
        if (tag is null) return;

        mapper.Map(dto, tag);
        unitOfWork.ExpenseTags.Update(tag);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await unitOfWork.ExpenseTags.GetByIdAsync(id, cancellationToken);
        if (tag is null) return;

        unitOfWork.ExpenseTags.Remove(tag);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
