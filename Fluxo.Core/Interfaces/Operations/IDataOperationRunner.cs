namespace Fluxo.Core.Interfaces.Operations;

public interface IDataOperationRunner
{
    Task RunAsync(Func<IDataOperationScope, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task RunAsync(string performedProcess,
        Func<IDataOperationScope, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<TResult> RunAsync<TResult>(Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    Task<TResult> RunAsync<TResult>(string performedProcess,
        Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    Task RunInTransactionAsync(string performedProcess,
        Func<IDataOperationScope, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);

    Task<TResult> RunInTransactionAsync<TResult>(string performedProcess,
        Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
