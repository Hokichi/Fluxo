using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.Tests.TestDoubles;

public sealed class InlineDataOperationRunner(IUnitOfWork unitOfWork) : IDataOperationRunner
{
    public Task RunAsync(Func<IDataOperationScope, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return operation(new InlineDataOperationScope(unitOfWork), cancellationToken);
    }

    public Task RunAsync(string performedProcess,
        Func<IDataOperationScope, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        _ = performedProcess;
        return RunAsync(operation, cancellationToken);
    }

    public Task<TResult> RunAsync<TResult>(Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return operation(new InlineDataOperationScope(unitOfWork), cancellationToken);
    }

    public Task<TResult> RunAsync<TResult>(string performedProcess,
        Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        _ = performedProcess;
        return RunAsync(operation, cancellationToken);
    }

    private sealed class InlineDataOperationScope(IUnitOfWork scopedUnitOfWork) : IDataOperationScope
    {
        public IServiceProvider ServiceProvider { get; } =
            new ServiceCollection().AddSingleton(scopedUnitOfWork).BuildServiceProvider();

        public IUnitOfWork UnitOfWork { get; } = scopedUnitOfWork;

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
