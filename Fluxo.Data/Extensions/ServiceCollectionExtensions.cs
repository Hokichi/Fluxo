using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Data.Context;
using Fluxo.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFluxoData(this IServiceCollection services)
    {
        services.AddTransient<FluxoDbContext>(_ => new FluxoDbContextFactory().CreateDbContext([]));

        // UnitOfWork factory: all repositories share the same DbContext instance
        services.AddTransient<IUnitOfWork>(_ =>
        {
            var dbContext = new FluxoDbContextFactory().CreateDbContext([]);
            return new UnitOfWork(
                dbContext,
                new ExpenseRepository(dbContext),
                new ExpenseLogRepository(dbContext),
                new IncomeLogRepository(dbContext),
                new ExpenseTagRepository(dbContext),
                new SavingGoalRepository(dbContext),
                new SpendingSourceRepository(dbContext),
                new NotificationRepository(dbContext),
                new UserSettingsRepository(dbContext));
        });

        services.AddTransient<IExpenseRepository, ExpenseRepository>();
        services.AddTransient<IExpenseLogRepository, ExpenseLogRepository>();
        services.AddTransient<IIncomeLogRepository, IncomeLogRepository>();
        services.AddTransient<IExpenseTagRepository, ExpenseTagRepository>();
        services.AddTransient<ISavingGoalRepository, SavingGoalRepository>();
        services.AddTransient<ISpendingSourceRepository, SpendingSourceRepository>();
        services.AddTransient<INotificationRepository, NotificationRepository>();
        services.AddTransient<IUserSettingsRepository, UserSettingsRepository>();

        services.AddTransient<IRepository<Expense>, ExpenseRepository>();
        services.AddTransient<IRepository<ExpenseLog>, ExpenseLogRepository>();
        services.AddTransient<IRepository<IncomeLog>, IncomeLogRepository>();
        services.AddTransient<IRepository<ExpenseTag>, ExpenseTagRepository>();
        services.AddTransient<IRepository<SavingGoal>, SavingGoalRepository>();
        services.AddTransient<IRepository<SpendingSource>, SpendingSourceRepository>();
        services.AddTransient<IRepository<Notification>, NotificationRepository>();

        return services;
    }
}
