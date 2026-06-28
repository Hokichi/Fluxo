using AutoMapper;
using Fluxo.Core.DTO;
using Fluxo.Core.Entities;
using Fluxo.Core.Filters;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;

namespace Fluxo.Services.Persistence;

public sealed class TagService(IDataOperationRunner dataOperationRunner, IMapper mapper) : ITagService
{
    public async Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dataOperationRunner.RunAsync("load expense tags", async (scope, ct) =>
        {
            var tags = await scope.UnitOfWork.Tags.GetAllAsync(ct);
            return mapper.Map<IReadOnlyList<TagDto>>(tags);
        }, cancellationToken);
    }

    public async Task AddAsync(TagDto dto, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("create tag", async (scope, ct) =>
        {
            var tag = mapper.Map<Tag>(dto);
            tag.Id = 0;
            await scope.UnitOfWork.Tags.AddAsync(tag, ct);
            await scope.UnitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task UpdateAsync(TagDto dto, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("update tag", async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var tag = await unitOfWork.Tags.GetByIdAsync(dto.Id, ct);
            if (tag is null)
                return;

            mapper.Map(dto, tag);
            unitOfWork.Tags.Update(tag);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }

    public async Task RemoveAsync(int id, CancellationToken cancellationToken = default)
    {
        await dataOperationRunner.RunAsync("delete tag", async (scope, ct) =>
        {
            var unitOfWork = scope.UnitOfWork;
            var tag = await unitOfWork.Tags.GetByIdAsync(id, ct);
            if (tag is null)
                return;

            // If Expense rows still reference this tag, FK (Restrict) will reject delete.
            unitOfWork.Tags.Remove(tag);
            await unitOfWork.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
