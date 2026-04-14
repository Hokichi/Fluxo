using AutoMapper;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Mappings;
using Fluxo.Services;
using Fluxo.Services.Mappings;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Shell.Main;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fluxo.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFluxoPresentation(this IServiceCollection services)
    {
        var mapperConfig = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<EntityDtoProfile>();
            cfg.AddProfile<DtoViewModelProfile>();
        }, NullLoggerFactory.Instance);

        services.AddSingleton<IMapper>(_ => mapperConfig.CreateMapper());

        services.AddTransient<IExpenseService, ExpenseService>();
        services.AddTransient<IExpenseLogService, ExpenseLogService>();
        services.AddTransient<ISpendingSourceService, SpendingSourceService>();
        services.AddTransient<ITagService, TagService>();

        return services;
    }

    public static IServiceCollection AddUIData(this IServiceCollection services)
    {
        services.AddSingleton<MainVM>();
        services.AddSingleton<DayOfWeekVM>();
        services.AddTransient<ExpenseVM>();
        services.AddTransient<ExpenseLogVM>();
        services.AddTransient<IncomeLogVM>();
        services.AddTransient<ExpenseTagVM>();
        services.AddTransient<SavingGoalVM>();
        services.AddTransient<SpendingSourceVM>();
        services.AddTransient<UserSettingsVM>();

        services.AddSingleton<MainWindow>();

        return services;
    }
}
