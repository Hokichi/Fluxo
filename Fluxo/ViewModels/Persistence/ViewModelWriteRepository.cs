using AutoMapper;
using Fluxo.Core.Interfaces.Repositories;

namespace Fluxo.ViewModels.Persistence;

public sealed class ViewModelWriteRepository<TEntity, TViewModel>(IRepository<TEntity> repository, IMapper mapper)
    : IWriteRepository<TViewModel>
    where TEntity : class
    where TViewModel : class
{
    private readonly IRepository<TEntity> _repository = repository;
    private readonly IMapper _mapper = mapper;

    public async Task AddAsync(TViewModel entity, CancellationToken cancellationToken = default)
    {
        await _repository.AddAsync(_mapper.Map<TEntity>(entity), cancellationToken);
    }

    public void Update(TViewModel entity)
    {
        _repository.Update(_mapper.Map<TEntity>(entity));
    }

    public void Remove(TViewModel entity)
    {
        _repository.Remove(_mapper.Map<TEntity>(entity));
    }
}
