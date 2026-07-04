using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Data;
using Fluxo.Data.Context;
using Fluxo.Data.Extensions;
using Fluxo.Data.Repositories;
using Fluxo.Services.Persistence;
using Fluxo.Tests.TestDoubles;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class DataServiceRegistrationTests
{
#if DEBUG
    [Fact]
    public void AddFluxoData_EnablesSensitiveDataLoggingInDebug()
    {
        using var provider = new ServiceCollection()
            .AddSingleton(Substitute.For<ILogService>())
            .AddFluxoData()
            .BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();

        Assert.True(context.GetService<IDbContextOptions>()
            .Extensions.OfType<CoreOptionsExtension>().Single().IsSensitiveDataLoggingEnabled);
    }
#endif

    [Fact]
    public void AddFluxoData_RegistersExpectedLifetimes()
    {
        var services = new ServiceCollection();

        services.AddFluxoData();

        AssertLifetime<FluxoDbContext>(services, ServiceLifetime.Scoped);
        AssertLifetime<IUnitOfWork>(services, ServiceLifetime.Scoped);
        AssertLifetime<ITransactionRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<ITagRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<ISavingGoalRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IAccountRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRecurringTransactionRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<INotificationRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IUserSettingsRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IBudgetAllocationRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<Transaction>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<Tag>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<SavingGoal>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<Account>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<RecurringTransaction>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<Notification>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IDataOperationScopeFactory>(services, ServiceLifetime.Singleton);
        AssertLifetime<IDataOperationRunner>(services, ServiceLifetime.Singleton);

        services.AddScoped<IAppDataService>(provider =>
            new AppDataService(provider.GetRequiredService<IDataOperationRunner>()));
        services.AddSingleton(Substitute.For<ILogService>());
        using var provider = services.BuildServiceProvider();

        var recurringRepository = provider.GetService<IRecurringTransactionRepository>();
        Assert.NotNull(recurringRepository);

        var budgetAllocationRepository = provider.GetService<IBudgetAllocationRepository>();
        Assert.NotNull(budgetAllocationRepository);

        var appData = provider.GetRequiredService<IAppDataService>();
        Assert.NotNull(appData.GetRecurringTransactionsAsync());
        Assert.NotNull(appData.GetBudgetAllocationAsync());
    }

    [Fact]
    public async Task EnsureBudgetAllocationAsync_WhenCalledRepeatedlyBeforeSave_ReusesPendingRow()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<FluxoDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new FluxoDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        using var unitOfWork = CreateUnitOfWork(dbContext);
        var appData = new AppDataService(new InlineDataOperationRunner(unitOfWork));

        var first = await appData.EnsureBudgetAllocationAsync();
        var second = await appData.EnsureBudgetAllocationAsync();

        Assert.Same(first, second);
        Assert.Empty(dbContext.BudgetAllocation.Local);

        await appData.SaveChangesAsync();

        Assert.Single(await dbContext.BudgetAllocation.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_AppliesQueuedRootsInsideOperation()
    {
        var accounts = Substitute.For<IAccountRepository>();
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Accounts.Returns(accounts);
        var appData = new AppDataService(new InlineDataOperationRunner(unitOfWork));
        var account = new Account { Id = 4, Name = "Updated" };

        appData.UpdateAccount(account);

        accounts.DidNotReceive().Update(Arg.Any<Account>());

        await appData.SaveChangesAsync();

        accounts.Received(1).Update(account);
        await unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private static void AssertLifetime<TService>(
        IServiceCollection services,
        ServiceLifetime expectedLifetime)
    {
        var descriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(TService));
        Assert.NotNull(descriptor);
        Assert.Equal(expectedLifetime, descriptor!.Lifetime);
    }

    private static UnitOfWork CreateUnitOfWork(FluxoDbContext dbContext)
    {
        return new UnitOfWork(
            dbContext,
            new TransactionRepository(dbContext),
            new TagRepository(dbContext),
            new SavingGoalRepository(dbContext),
            new AccountRepository(dbContext),
            new RecurringTransactionRepository(dbContext),
            new NotificationRepository(dbContext),
            new UserSettingsRepository(dbContext),
            new BudgetAllocationRepository(dbContext));
    }
}
