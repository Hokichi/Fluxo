using AutoMapper;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.ViewModels.Persistence;

public sealed class ExpenseLogViewModelReadRepository<TViewModel>(
    IExpenseLogRepository repository,
    IMapper mapper)
    : IExpenseLogReadRepository<TViewModel>
    where TViewModel : class
{
    private readonly IMapper _mapper = mapper;
    private readonly IExpenseLogRepository _repository = repository;

    public async Task<IReadOnlyList<TViewModel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetAllAsync(cancellationToken);
        return _mapper.Map<IReadOnlyList<TViewModel>>(entities);
    }

    public async Task<TViewModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(id, cancellationToken);
        return entity is null ? null : _mapper.Map<TViewModel>(entity);
    }

    public async Task<IReadOnlyList<TViewModel>> GetByDayAsync(DateTime day,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByDayAsync(day, cancellationToken);
        return _mapper.Map<IReadOnlyList<TViewModel>>(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetByWeekAsync(DateTime startOfWeek, DateTime endOfWeek,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByWeekAsync(startOfWeek, endOfWeek, cancellationToken);
        return _mapper.Map<IReadOnlyList<TViewModel>>(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetByMonthAsync(int month,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByMonthAsync(month, cancellationToken);
        return _mapper.Map<IReadOnlyList<TViewModel>>(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetByCategoryAsync(ExpenseCategory category,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByCategoryAsync(category, cancellationToken);
        return _mapper.Map<IReadOnlyList<TViewModel>>(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetBySpendingSourceIdAsync(int spendingSourceId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetBySpendingSourceIdAsync(spendingSourceId, cancellationToken);
        return _mapper.Map<IReadOnlyList<TViewModel>>(entities);
    }
}
