using AutoMapper;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;

namespace Fluxo.ViewModels.Persistence;

public sealed class ExpenseViewModelReadRepository<TViewModel>(IExpenseRepository repository, FluxoDbContext dbContext, IMapper mapper)
    : IExpenseReadRepository<TViewModel>
    where TViewModel : class
{
    private readonly IExpenseRepository _repository = repository;
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

    public async Task<IReadOnlyList<TViewModel>> GetByDayAsync(DateTime day, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByDayAsync(day, cancellationToken);
        return MapListWithIds(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetByWeekAsync(DateTime startOfWeek, DateTime endOfWeek, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByWeekAsync(startOfWeek, endOfWeek, cancellationToken);
        return MapListWithIds(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetByMonthAsync(int month, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByMonthAsync(month, cancellationToken);
        return MapListWithIds(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetByKindAsync(ExpenseKind kind, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByKindAsync(kind, cancellationToken);
        return MapListWithIds(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetByCategoryAsync(ExpenseCategory category, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByCategoryAsync(category, cancellationToken);
        return MapListWithIds(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetByTagIdAsync(int tagId, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByTagIdAsync(tagId, cancellationToken);
        return MapListWithIds(entities);
    }

    private IReadOnlyList<TViewModel> MapListWithIds(IReadOnlyList<Core.Entities.Expense> entities)
    {
        return entities.Select(MapWithId).ToList();
    }

    private TViewModel MapWithId(Core.Entities.Expense entity)
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
