using Fluxo.Core.Exceptions;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.Data.Operations;

public sealed class DataOperationRunner(IDataOperationScopeFactory scopeFactory, ILogService logService)
    : IDataOperationRunner
{
    public Task RunInTransactionAsync(string performedProcess,
        Func<IDataOperationScope, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return RunAsync(performedProcess, async (scope, ct) =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            await operation(scope, ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);
    }

    public Task<TResult> RunInTransactionAsync<TResult>(string performedProcess,
        Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return RunAsync(performedProcess, async (scope, ct) =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            var result = await operation(scope, ct);
            await transaction.CommitAsync(ct);
            return result;
        }, cancellationToken);
    }

    public async Task RunAsync(Func<IDataOperationScope, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        await RunAsync("complete data operation", operation, cancellationToken);
    }

    public async Task RunAsync(string performedProcess,
        Func<IDataOperationScope, CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(performedProcess);

        await using var scope = await scopeFactory.CreateAsync(cancellationToken);

        try
        {
            await operation(scope, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DataOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logService.LogFailureForProcess(exception, performedProcess);
            throw new DataOperationException(
                performedProcess,
                logService.CreateFailureMessage(performedProcess),
                exception);
        }
    }

    public async Task<TResult> RunAsync<TResult>(Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync("complete data operation", operation, cancellationToken);
    }

    public async Task<TResult> RunAsync<TResult>(string performedProcess,
        Func<IDataOperationScope, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentException.ThrowIfNullOrWhiteSpace(performedProcess);

        await using var scope = await scopeFactory.CreateAsync(cancellationToken);

        try
        {
            return await operation(scope, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DataOperationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logService.LogFailureForProcess(exception, performedProcess);
            throw new DataOperationException(
                performedProcess,
                logService.CreateFailureMessage(performedProcess),
                exception);
        }
    }
}
