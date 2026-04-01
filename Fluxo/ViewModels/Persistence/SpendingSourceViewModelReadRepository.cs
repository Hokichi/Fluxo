using AutoMapper;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;

namespace Fluxo.ViewModels.Persistence;

public sealed class SpendingSourceViewModelReadRepository<TViewModel>(ISpendingSourceRepository repository, FluxoDbContext dbContext, IMapper mapper)
    : ISpendingSourceReadRepository<TViewModel>
    where TViewModel : class
{
    private readonly ISpendingSourceRepository _repository = repository;
    private readonly FluxoDbContext _dbContext = dbContext;
    private readonly IMapper _mapper = mapper;

    public async Task<IReadOnlyList<TViewModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        return MapListWithIds(entities);
    }

    public async Task<TViewModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : MapWithId(entity);
    }

    public async Task<IReadOnlyList<TViewModel>> GetByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByDateAsync(date, cancellationToken);
        return MapListWithIds(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetBySourceTypeAsync(SpendingSourceType sourceType, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetBySourceTypeAsync(sourceType, cancellationToken);
        return MapListWithIds(entities);
    }

    private IReadOnlyList<TViewModel> MapListWithIds(IReadOnlyList<SpendingSource> entities)
    {
        return entities.Select(MapWithId).ToList();
    }

    private TViewModel MapWithId(SpendingSource entity)
    {
        var viewModel = _mapper.Map<TViewModel>(entity);
        TrySetId(viewModel, _dbContext.Entry(entity).Property<int>("Id").CurrentValue);
        return viewModel;
    }

    private static void TrySetId(TViewModel viewModel, int id)
    {
        var property = typeof(TViewModel).GetProperty("Id");
        if (property?.CanWrite == true && property.PropertyType == typeof(int))
            property.SetValue(viewModel, id);
    }
}
