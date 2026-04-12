using AutoMapper;
using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.ViewModels.Persistence;

public sealed class ExpenseTagViewModelReadRepository<TViewModel>(
    IExpenseTagRepository repository,
    IMapper mapper)
    : IExpenseTagReadRepository<TViewModel>
    where TViewModel : class
{
    private readonly IMapper _mapper = mapper;
    private readonly IExpenseTagRepository _repository = repository;

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

    public async Task<IReadOnlyList<(TViewModel Tag, int Count)>> GetTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default)
    {
        var results = await _repository.GetTagsByCountDescendingAsync(cancellationToken);
        return results
            .Select(item => (Tag: _mapper.Map<TViewModel>(item.Tag), item.Count))
            .Where(c => c.Count > 0)
            .ToList();
    }

    public async Task<IReadOnlyList<(TViewModel Tag, int Count)>> GetTodayTagsByCountDescendingAsync(
        CancellationToken cancellationToken = default)
    {
        var results = await _repository.GetTodayTagsByCountDescendingAsync(cancellationToken);
        return results
            .Select(item => (Tag: _mapper.Map<TViewModel>(item.Tag), item.Count))
            .ToList();
    }
}