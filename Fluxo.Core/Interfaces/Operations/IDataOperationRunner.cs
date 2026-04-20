namespace Fluxo.Core.Interfaces.Operations;

public interface IDataOperationRunner
{
    Task RunAsync(Func<IDataOperationScope, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<TResult> RunAsync<TResult>(Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
