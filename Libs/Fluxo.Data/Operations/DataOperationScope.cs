using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.Data.Operations;

public sealed class DataOperationScope(AsyncServiceScope scope) : IDataOperationScope
{
    public IServiceProvider ServiceProvider => scope.ServiceProvider;

    public IUnitOfWork UnitOfWork => scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    public ValueTask DisposeAsync()
    {
        return scope.DisposeAsync();
    }
}
