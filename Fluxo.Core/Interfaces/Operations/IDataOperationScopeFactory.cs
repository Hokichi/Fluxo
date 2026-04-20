namespace Fluxo.Core.Interfaces.Operations;

public interface IDataOperationScopeFactory
{
    ValueTask<IDataOperationScope> CreateAsync(CancellationToken cancellationToken = default);
}
