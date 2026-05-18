using AutoMapper;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Mappings;
using Fluxo.Services.Mappings;
using Fluxo.Services.Persistence;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Notifications;
using Fluxo.Services.Ui;
using Fluxo.Services.Logging;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Fluxo.ViewModels.Popups.Planning;
using Fluxo.ViewModels.Popups.Settings;
using Fluxo.Services.Updates;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Popups;
using Fluxo.Views.Popups.Planning;
using Fluxo.Views.Popups.Settings;
using Fluxo.Views.Shell.Main;
using Fluxo.Views.Shell.Wizard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using AnalyticsVM = Fluxo.ViewModels.Shell.Main.AnalyticsVM;
using BudgetAllocationPanelVM = Fluxo.ViewModels.Shell.Main.BudgetAllocationPanelVM;
using DaySpinnerVM = Fluxo.ViewModels.Shell.Main.DaySpinnerVM;
using MainViewModeToggleVM = Fluxo.ViewModels.Shell.Main.MainViewModeToggleVM;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;
using NotificationPanelVM = Fluxo.ViewModels.Shell.Main.NotificationPanelVM;
using SavingGoalsPanelVM = Fluxo.ViewModels.Shell.Main.SavingGoalsPanelVM;
using SpentAllowancePanelVM = Fluxo.ViewModels.Shell.Main.SpentAllowancePanelVM;
using QuickSetupWizardVM = Fluxo.ViewModels.Shell.QuickSetupWizard.QuickSetupWizardVM;

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
        services.AddTransient<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IAppDataService, AppDataService>();

        return services;
    }

    public static IServiceCollection AddUIData(this IServiceCollection services)
    {
        services.AddSingleton<IMessenger>(_ => WeakReferenceMessenger.Default);
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ILogService, FluxoLogService>();
        services.AddSingleton<IUiSettleAwaiter, UiSettleAwaiter>();
        services.AddSingleton<IStartupRegistrationService, StartupRegistrationService>();
        services.AddSingleton<IAppUpdateService, AppUpdateService>();
        services.AddSingleton<IAppUpdateLifecycleService, AppUpdateLifecycleService>();
        services.AddSingleton<IAppUpdateInteractionService, AppUpdateInteractionService>();
        services.AddSingleton<IStartupUpdateNotificationService, StartupUpdateNotificationService>();

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
        services.AddTransient<NotificationChecklistActionVM>();
        services.AddTransient<GoalDeadlineActionVM>();
        services.AddTransient<PlanningPopupVM>();
        services.AddTransient<PlanningReportVM>();
        services.AddTransient<AnalyticsVM>();
        services.AddTransient<SettingsBudgetTabVM>();
        services.AddTransient<SettingsSourcesTabVM>();
        services.AddTransient<SettingsFixedExpensesTabVM>();
        services.AddTransient<SettingsGoalsTabVM>();
        services.AddTransient<SettingsTagsTabVM>();
        services.AddTransient<SettingsPersonalizationTabVM>();
        services.AddTransient<SettingsVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardGreetingPageVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardNamePageVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardMiddlePageVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardLoadingPageVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardFinalPageVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardSpendingSourcesVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardFixedExpensesVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardSavingGoalsVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardBudgetAllocationVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardNotificationVM>();
        services.AddTransient<ViewModels.Shell.QuickSetupWizard.QuickSetupWizardSummaryVM>();
        services.AddTransient<QuickSetupWizardVM>();

        services.AddTransient<QuickAddPopup>();
        services.AddTransient<SpendingSourcesListPopup>();
        services.AddTransient<AddSpendingSourcePopup>();
        services.AddTransient<AddFixedExpensePopup>();
        services.AddTransient<AddSavingGoalPopup>();
        services.AddTransient<NotificationChecklistActionPopup>();
        services.AddTransient<GoalDeadlineActionPopup>();
        services.AddTransient<SettingsPopup>();
        services.AddTransient<QuickSetupWizard>();
        services.AddTransient<PlanningPopup>();
        services.AddTransient<PlanningReportPopup>();
        services.AddTransient<Analytics>();
        services.AddTransient<INotificationGroupingService, NotificationGroupingService>();
        services.AddTransient<INotificationActionService, NotificationActionService>();
        services.AddTransient<IStartupNotificationSummaryService, StartupNotificationSummaryService>();

        services.AddSingleton<MainWindow>();

        return services;
    }
}
