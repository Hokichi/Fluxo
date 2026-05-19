using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Data.Context;
using Fluxo.Data.Extensions;
using Fluxo.Services.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fluxo.Tests.Infrastructure;

public sealed class DataServiceRegistrationTests
{
    [Fact]
    public void AddFluxoData_RegistersExpectedLifetimes()
    {
        var services = new ServiceCollection();

        services.AddFluxoData();

        AssertLifetime<FluxoDbContext>(services, ServiceLifetime.Scoped);
        AssertLifetime<IUnitOfWork>(services, ServiceLifetime.Scoped);
        AssertLifetime<IExpenseRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IExpenseLogRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IIncomeLogRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IExpenseTagRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<ISavingGoalRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<ISpendingSourceRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRecurringTransactionRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<INotificationRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IUserSettingsRepository>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<Expense>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<ExpenseLog>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<IncomeLog>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<ExpenseTag>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<SavingGoal>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<SpendingSource>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<RecurringTransaction>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IRepository<Notification>>(services, ServiceLifetime.Scoped);
        AssertLifetime<IDataOperationScopeFactory>(services, ServiceLifetime.Singleton);
        AssertLifetime<IDataOperationRunner>(services, ServiceLifetime.Singleton);

        services.AddScoped<IAppDataService, AppDataService>();
        using var provider = services.BuildServiceProvider();

        var recurringRepository = provider.GetService<IRecurringTransactionRepository>();
        Assert.NotNull(recurringRepository);

        var appData = provider.GetRequiredService<IAppDataService>();
        Assert.NotNull(appData.GetRecurringTransactionsAsync());
    }

    private static void AssertLifetime<TService>(
        IServiceCollection services,
        ServiceLifetime expectedLifetime)
    {
        var descriptor = services.LastOrDefault(sd => sd.ServiceType == typeof(TService));
        Assert.NotNull(descriptor);
        Assert.Equal(expectedLifetime, descriptor!.Lifetime);
    }
}
