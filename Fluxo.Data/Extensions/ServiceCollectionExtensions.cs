using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Data.Context;
using Fluxo.Data.Operations;
using Fluxo.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFluxoData(this IServiceCollection services)
    {
        services.AddDbContext<FluxoDbContext>((serviceProvider, optionsBuilder) =>
        {
            var logService = serviceProvider.GetService<ILogService>();

            optionsBuilder.UseSqlite(
                FluxoDbContextFactory.BuildConnectionString(),
                sqliteOptions => sqliteOptions.MigrationsAssembly("Fluxo"));

            if (logService is not null)
            {
                optionsBuilder.LogTo(
                    message => logService.LogInformation($"EF Core: {message}"),
                    [DbLoggerCategory.Database.Name, DbLoggerCategory.Update.Name, DbLoggerCategory.Infrastructure.Name],
                    LogLevel.Information);
            }
        }, ServiceLifetime.Scoped);

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IExpenseLogRepository, ExpenseLogRepository>();
        services.AddScoped<IIncomeLogRepository, IncomeLogRepository>();
        services.AddScoped<IExpenseTagRepository, ExpenseTagRepository>();
        services.AddScoped<ISavingGoalRepository, SavingGoalRepository>();
        services.AddScoped<ISpendingSourceRepository, SpendingSourceRepository>();
        services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IUserSettingsRepository, UserSettingsRepository>();

        services.AddScoped<IRepository<Expense>, ExpenseRepository>();
        services.AddScoped<IRepository<ExpenseLog>, ExpenseLogRepository>();
        services.AddScoped<IRepository<IncomeLog>, IncomeLogRepository>();
        services.AddScoped<IRepository<ExpenseTag>, ExpenseTagRepository>();
        services.AddScoped<IRepository<SavingGoal>, SavingGoalRepository>();
        services.AddScoped<IRepository<SpendingSource>, SpendingSourceRepository>();
        services.AddScoped<IRepository<RecurringTransaction>, RecurringTransactionRepository>();
        services.AddScoped<IRepository<Notification>, NotificationRepository>();

        services.AddSingleton<IDataOperationScopeFactory, DataOperationScopeFactory>();
        services.AddSingleton<IDataOperationRunner, DataOperationRunner>();

        return services;
    }
}
