using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Mappings;
using Fluxo.Services.Mappings;
using Fluxo.Services.Persistence;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Planning;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Popups;
using Fluxo.Views.Popups.Planning;
using Fluxo.Views.Popups.Settings;
using Fluxo.Views.Shell.Main;
using Fluxo.Views.Shell.Wizard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using BudgetAllocationPanelVM = Fluxo.ViewModels.Shell.Main.BudgetAllocationPanelVM;
using DaySpinnerVM = Fluxo.ViewModels.Shell.Main.DaySpinnerVM;
using MainViewModeToggleVM = Fluxo.ViewModels.Shell.Main.MainViewModeToggleVM;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;
using NotificationPanelVM = Fluxo.ViewModels.Shell.Main.NotificationPanelVM;
using SavingGoalsPanelVM = Fluxo.ViewModels.Shell.Main.SavingGoalsPanelVM;
using SpentAllowancePanelVM = Fluxo.ViewModels.Shell.Main.SpentAllowancePanelVM;
using StartupWizardVM = Fluxo.ViewModels.Shell.StartupWizard.StartupWizardVM;

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
        services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
        services.AddSingleton<IDialogService, DialogService>();

        services.AddSingleton<MainVM>();
        services.AddSingleton<DaySpinnerVM>();
        services.AddSingleton<MainViewModeToggleVM>();
        services.AddSingleton<BudgetAllocationPanelVM>();
        services.AddSingleton<SpentAllowancePanelVM>();
        services.AddSingleton<NotificationPanelVM>();
        services.AddSingleton<SavingGoalsPanelVM>();
        services.AddSingleton<DayOfWeekVM>();
        services.AddTransient<ExpenseVM>();
        services.AddTransient<ExpenseLogVM>();
        services.AddTransient<IncomeLogVM>();
        services.AddTransient<ExpenseTagVM>();
        services.AddTransient<SavingGoalVM>();
        services.AddTransient<SpendingSourceVM>();
        services.AddTransient<UserSettingsVM>();
        services.AddTransient<QuickAddVM>();
        services.AddTransient<AddSpendingSourceVM>();
        services.AddTransient<AddFixedExpenseVM>();
        services.AddTransient<AddSavingGoalVM>();
        services.AddTransient<PlanningPopupVM>();
        services.AddTransient<PlanningReportVM>();
        services.AddTransient<SettingsBudgetTabVM>();
        services.AddTransient<SettingsSourcesTabVM>();
        services.AddTransient<SettingsFixedExpensesTabVM>();
        services.AddTransient<SettingsGoalsTabVM>();
        services.AddTransient<SettingsTagsTabVM>();
        services.AddTransient<SettingsPersonalizationTabVM>();
        services.AddTransient<SettingsVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardGreetingPageVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardNamePageVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardMiddlePageVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardLoadingPageVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardFinalPageVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardSpendingSourcesVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardFixedExpensesVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardSavingGoalsVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardBudgetAllocationVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardNotificationVM>();
        services.AddTransient<ViewModels.Shell.StartupWizard.StartupWizardSummaryVM>();
        services.AddTransient<StartupWizardVM>();

        services.AddTransient<QuickAddPopup>();
        services.AddTransient<QuickSearchPopup>();
        services.AddTransient<SpendingSourcesListPopup>();
        services.AddTransient<AddSpendingSourcePopup>();
        services.AddTransient<AddFixedExpensePopup>();
        services.AddTransient<AddSavingGoalPopup>();
        services.AddTransient<SettingsPopup>();
        services.AddTransient<StartupWizardPopup>();
        services.AddTransient<PlanningPopup>();
        services.AddTransient<PlanningReportPopup>();

        services.AddSingleton<MainWindow>();

        return services;
    }
}
