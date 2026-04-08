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
        services.AddTransient<IUnitOfWork, UnitOfWork>();

        services.AddTransient<IExpenseRepository, ExpenseRepository>();
        services.AddTransient<IExpenseLogRepository, ExpenseLogRepository>();
        services.AddTransient<IIncomeLogRepository, IncomeLogRepository>();
        services.AddTransient<IExpenseTagRepository, ExpenseTagRepository>();
        services.AddTransient<ISavingGoalRepository, SavingGoalRepository>();
        services.AddTransient<ISpendingSourceRepository, SpendingSourceRepository>();
        services.AddTransient<IUserSettingsRepository, UserSettingsRepository>();

        services.AddTransient<IRepository<Expense>, ExpenseRepository>();
        services.AddTransient<IRepository<ExpenseLog>, ExpenseLogRepository>();
        services.AddTransient<IRepository<IncomeLog>, IncomeLogRepository>();
        services.AddTransient<IRepository<ExpenseTag>, ExpenseTagRepository>();
        services.AddTransient<IRepository<SavingGoal>, SavingGoalRepository>();
        services.AddTransient<IRepository<SpendingSource>, SpendingSourceRepository>();

        return services;
    }
}
