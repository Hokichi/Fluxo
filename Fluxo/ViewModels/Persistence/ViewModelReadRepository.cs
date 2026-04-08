using AutoMapper;
using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.ViewModels.Persistence;

public sealed class ViewModelReadRepository<TEntity, TViewModel>(IRepository<TEntity> repository, IMapper mapper)
    : IReadRepository<TViewModel>
    where TEntity : class
    where TViewModel : class
{
    private readonly IMapper _mapper = mapper;
    private readonly IRepository<TEntity> _repository = repository;

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
}