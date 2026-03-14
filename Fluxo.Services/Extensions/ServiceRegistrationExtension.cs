using Microsoft.Extensions.DependencyInjection;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Services.Persistence;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Data.Repositories;

namespace Fluxo.Services.Extensions;

public static class ServiceRegistrationExtension
{
    public static IServiceCollection RegisterServices(this IServiceCollection services)
    {
        // ── Repositories ──────────────────────────────────────────────────────
        services.AddScoped<IIncomeSourceRepository, IncomeSourceRepository>();
        services.AddScoped<IIncomeEntryRepository, IncomeEntryRepository>();
        services.AddScoped<IBnplSourceRepository, BnplSourceRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IFixedExpenseRepository, FixedExpenseRepository>();
        services.AddScoped<IFixedExpenseHistoryRepository, FixedExpenseHistoryRepository>();
        services.AddScoped<ISavingsAccountRepository, SavingsAccountRepository>();
        services.AddScoped<ISavingsGoalRepository, SavingsGoalRepository>();
        services.AddScoped<IBudgetConfigRepository, BudgetConfigRepository>();
        services.AddScoped<ITagRepository, TagRepository>();
        services.AddScoped<IAppSettingRepository, AppSettingRepository>();

        // ── Services ──────────────────────────────────────────────────────────
        // AppSettingService first — others depend on it.
        services.AddScoped<IAppSettingService, AppSettingService>();

        services.AddScoped<IIncomeService, IncomeService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<ISavingsService, SavingsService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IFixedExpenseService, FixedExpenseService>();
        services.AddScoped<IBnplService, BnplService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<ITrendService, TrendService>();
        services.AddScoped<INotificationService, NotificationService>();

        // DashboardService is the main screen's single entry point.
        services.AddScoped<IDashboardService, DashboardService>();
        return services;
    }
}