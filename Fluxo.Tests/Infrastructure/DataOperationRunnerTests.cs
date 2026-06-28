using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Data.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class DataOperationRunnerTests
{
    [Fact]
    public async Task RunAsync_UsesSharedScopedInstancesWithinSingleOperation()
    {
        using var serviceProvider = CreateServiceProvider();

        var runner = serviceProvider.GetRequiredService<IDataOperationRunner>();

        await runner.RunAsync((scope, _) =>
        {
            var unitOfWorkFromScope = scope.UnitOfWork;
            var unitOfWorkFromResolver = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var transactionsFromScope = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
            var transactionsFromUnitOfWork = unitOfWorkFromScope.Transactions;

            Assert.Same(unitOfWorkFromScope, unitOfWorkFromResolver);
            Assert.Same(transactionsFromScope, transactionsFromUnitOfWork);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task RunAsync_CreatesDifferentUnitOfWorkForDifferentOperations()
    {
        using var serviceProvider = CreateServiceProvider();

        var runner = serviceProvider.GetRequiredService<IDataOperationRunner>();
        IUnitOfWork? first = null;
        IUnitOfWork? second = null;

        await runner.RunAsync((scope, _) =>
        {
            first = scope.UnitOfWork;
            return Task.CompletedTask;
        });

        await runner.RunAsync((scope, _) =>
        {
            second = scope.UnitOfWork;
            return Task.CompletedTask;
        });

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.NotSame(first!.Transactions, second!.Transactions);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        return new ServiceCollection()
            .AddSingleton(Substitute.For<ILogService>())
            .AddFluxoData()
            .BuildServiceProvider();
    }
}
