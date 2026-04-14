using AutoMapper;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Core.Interfaces.Repositories;
using Fluxo.Mappings;
using Fluxo.Services.Persistence;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Persistence;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Shell;
using Fluxo.Views.Shell.Main;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fluxo.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFluxoPresentation(this IServiceCollection services)
    {
        var mapperConfigurationExpression = new MapperConfigurationExpression();
        mapperConfigurationExpression.AddProfile<EntityViewModelProfile>();
        var mapperConfiguration = new MapperConfiguration(mapperConfigurationExpression, NullLoggerFactory.Instance);

        services.AddSingleton<IMapper>(_ => mapperConfiguration.CreateMapper());

        // ViewModel read repositories
        services.AddTransient<IExpenseReadRepository<ExpenseVM>>(serviceProvider =>
            new ExpenseViewModelReadRepository<ExpenseVM>(
                serviceProvider.GetRequiredService<IExpenseRepository>(),
                serviceProvider.GetRequiredService<IMapper>()));
        services.AddTransient<IExpenseLogReadRepository<ExpenseLogVM>>(serviceProvider =>
            new ExpenseLogViewModelReadRepository<ExpenseLogVM>(
                serviceProvider.GetRequiredService<IExpenseLogRepository>(),
                serviceProvider.GetRequiredService<IMapper>()));
        services.AddTransient<IIncomeLogReadRepository<IncomeLogVM>>(serviceProvider =>
            new IncomeLogViewModelReadRepository<IncomeLogVM>(
                serviceProvider.GetRequiredService<IIncomeLogRepository>(),
                serviceProvider.GetRequiredService<IMapper>()));
        services.AddTransient<IExpenseTagReadRepository<ExpenseTagVM>>(serviceProvider =>
            new ExpenseTagViewModelReadRepository<ExpenseTagVM>(
                serviceProvider.GetRequiredService<IExpenseTagRepository>(),
                serviceProvider.GetRequiredService<IMapper>()));
        services.AddTransient<IReadRepository<SavingGoalVM>>(serviceProvider =>
            new ViewModelReadRepository<SavingGoal, SavingGoalVM>(
                serviceProvider.GetRequiredService<IRepository<SavingGoal>>(),
                serviceProvider.GetRequiredService<IMapper>()));
        services.AddTransient<ISpendingSourceReadRepository<SpendingSourceVM>>(serviceProvider =>
            new SpendingSourceViewModelReadRepository<SpendingSourceVM>(
                serviceProvider.GetRequiredService<ISpendingSourceRepository>(),
                serviceProvider.GetRequiredService<IMapper>()));

        // ViewModel write repositories
        services.AddTransient<IWriteRepository<ExpenseVM>>(serviceProvider =>
            new ViewModelWriteRepository<Expense, ExpenseVM>(
                serviceProvider.GetRequiredService<IRepository<Expense>>(),
                serviceProvider.GetRequiredService<IMapper>()));
        services.AddTransient<IWriteRepository<ExpenseLogVM>>(serviceProvider =>
            new ViewModelWriteRepository<ExpenseLog, ExpenseLogVM>(
                serviceProvider.GetRequiredService<IRepository<ExpenseLog>>(),
                serviceProvider.GetRequiredService<IMapper>()));
        services.AddTransient<IWriteRepository<IncomeLogVM>>(serviceProvider =>
            new ViewModelWriteRepository<IncomeLog, IncomeLogVM>(
                serviceProvider.GetRequiredService<IRepository<IncomeLog>>(),
                serviceProvider.GetRequiredService<IMapper>()));
        services.AddTransient<IWriteRepository<ExpenseTagVM>>(serviceProvider =>
            new ViewModelWriteRepository<ExpenseTag, ExpenseTagVM>(
                serviceProvider.GetRequiredService<IRepository<ExpenseTag>>(),
                serviceProvider.GetRequiredService<IMapper>()));
        services.AddTransient<IWriteRepository<SavingGoalVM>>(serviceProvider =>
            new ViewModelWriteRepository<SavingGoal, SavingGoalVM>(
                serviceProvider.GetRequiredService<IRepository<SavingGoal>>(),
                serviceProvider.GetRequiredService<IMapper>()));
        services.AddTransient<IWriteRepository<SpendingSourceVM>>(serviceProvider =>
            new ViewModelWriteRepository<SpendingSource, SpendingSourceVM>(
                serviceProvider.GetRequiredService<IRepository<SpendingSource>>(),
                serviceProvider.GetRequiredService<IMapper>()));

        // ViewModel unit of work compositions
        services
            .AddTransient<
                IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM,
                    SpendingSourceVM>, EntityViewModelReadUnitOfWork>();
        services
            .AddTransient<
                IViewModelWriteUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM, SavingGoalVM,
                    SpendingSourceVM>, EntityViewModelWriteUnitOfWork>();

        // MainWindow is Singleton but needs fresh IUnitOfWork (Transient) per popup,
        // so a factory delegate bridges the lifetime mismatch.
        services.AddSingleton<Func<IUnitOfWork>>(serviceProvider =>
            serviceProvider.GetRequiredService<IUnitOfWork>);
        services.AddSingleton<Func<IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM, ExpenseTagVM,
            SavingGoalVM, SpendingSourceVM>>>(serviceProvider =>
            serviceProvider.GetRequiredService<IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, IncomeLogVM,
                ExpenseTagVM, SavingGoalVM, SpendingSourceVM>>);

        // Cleanup service
        services.AddTransient<IExpenseCleanupService, ExpenseCleanupService>();

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