using AutoMapper;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.ViewModels.Persistence;

public sealed class SpendingSourceViewModelReadRepository<TViewModel>(
    ISpendingSourceRepository repository,
    IMapper mapper)
    : ISpendingSourceReadRepository<TViewModel>
    where TViewModel : class
{
    private readonly IMapper _mapper = mapper;
    private readonly ISpendingSourceRepository _repository = repository;

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

    public async Task<IReadOnlyList<TViewModel>> GetByDateAsync(DateTime date,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetByDateAsync(date, cancellationToken);
        return _mapper.Map<IReadOnlyList<TViewModel>>(entities);
    }

    public async Task<IReadOnlyList<TViewModel>> GetBySourceTypeAsync(SpendingSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetBySourceTypeAsync(sourceType, cancellationToken);
        return _mapper.Map<IReadOnlyList<TViewModel>>(entities);
    }
}