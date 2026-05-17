using Fluxo.Core.Interfaces.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.Data.Operations;

public sealed class DataOperationScopeFactory(IServiceScopeFactory serviceScopeFactory) : IDataOperationScopeFactory
{
    public ValueTask<IDataOperationScope> CreateAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IDataOperationScope>(
            new DataOperationScope(serviceScopeFactory.CreateAsyncScope()));
    }
}
