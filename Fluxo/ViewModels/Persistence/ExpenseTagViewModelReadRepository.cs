using AutoMapper;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;

namespace Fluxo.ViewModels.Persistence;

public sealed class ExpenseTagViewModelReadRepository<TViewModel>(
    IExpenseTagRepository repository,
    FluxoDbContext dbContext,
    IMapper mapper)
    : IExpenseTagReadRepository<TViewModel>
    where TViewModel : class
{
    private readonly FluxoDbContext _dbContext = dbContext;
    private readonly IMapper _mapper = mapper;
    private readonly IExpenseTagRepository _repository = repository;

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

    public async Task<IReadOnlyList<(TViewModel Tag, int Count)>> GetTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default)
    {
        var results = await _repository.GetTagsByCountDescendingAsync(cancellationToken);
        return results
            .Select(item => (Tag: MapWithId(item.Tag), item.Count))
            .Where(c => c.Count > 0)
            .ToList();
    }

    public async Task<IReadOnlyList<(TViewModel Tag, int Count)>> GetTodayTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default)
    {
        var results = await _repository.GetTodayTagsByCountDescendingAsync(cancellationToken);
        return results
            .Select(item => (Tag: MapWithId(item.Tag), item.Count))
            .ToList();
    }

    private IReadOnlyList<TViewModel> MapListWithIds(IReadOnlyList<ExpenseTag> entities)
    {
        return entities.Select(MapWithId).ToList();
    }

    private TViewModel MapWithId(ExpenseTag entity)
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