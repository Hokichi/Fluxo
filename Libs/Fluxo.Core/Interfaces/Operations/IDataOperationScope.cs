using Fluxo.Core.Interfaces;

namespace Fluxo.Core.Interfaces.Operations;

public interface IDataOperationScope : IAsyncDisposable
{
    IServiceProvider ServiceProvider { get; }
    IUnitOfWork UnitOfWork { get; }
}
