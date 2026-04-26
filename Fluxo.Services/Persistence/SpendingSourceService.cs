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
}
