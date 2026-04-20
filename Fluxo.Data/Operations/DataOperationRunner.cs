using Fluxo.Core.Interfaces.Operations;

namespace Fluxo.Data.Operations;

public sealed class DataOperationRunner(IDataOperationScopeFactory scopeFactory) : IDataOperationRunner
{
    public async Task RunAsync(Func<IDataOperationScope, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var scope = await scopeFactory.CreateAsync(cancellationToken);
        await operation(scope, cancellationToken);
    }

    public async Task<TResult> RunAsync<TResult>(Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using var scope = await scopeFactory.CreateAsync(cancellationToken);
        return await operation(scope, cancellationToken);
    }
}
