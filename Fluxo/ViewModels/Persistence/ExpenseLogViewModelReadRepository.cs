using AutoMapper;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;

namespace Fluxo.ViewModels.Persistence;

public sealed class ExpenseLogViewModelReadRepository<TViewModel>(IExpenseLogRepository repository, FluxoDbContext dbContext, IMapper mapper)
    : IExpenseLogReadRepository<TViewModel>
    where TViewModel : class
{
    private readonly IExpenseLogRepository _repository = repository;
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

    public async Task<IReadOnlyList<TViewModel>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByCategoryAsync(category, cancellationToken);
        return MapListWithIds(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetBySpendingSourceIdAsync(spendingSourceId, cancellationToken);
        return MapListWithIds(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetTodayByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetTodayByCategoryAsync(category, cancellationToken);
        return MapListWithIds(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetTodayBySpendingSourceIdAsync(int spendingSourceId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetTodayBySpendingSourceIdAsync(spendingSourceId, cancellationToken);
        return MapListWithIds(entities);
    }

    private IReadOnlyList<TViewModel> MapListWithIds(IReadOnlyList<ExpenseLog> entities)
    {
        return entities.Select(MapWithId).ToList();
    }

    private TViewModel MapWithId(ExpenseLog entity)
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
