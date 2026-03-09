using System.Configuration;
using System.Data;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Fluxo.Services.Extensions;
using Fluxo.ViewModels.Shell;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Controls;
using Fluxo.Views.Shell;

namespace Fluxo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;

        public App()
        {
            // Build the service provider
            var services = new ServiceCollection();

            // Register all services
            services.RegisterServices();

            // Register all ViewModels
            services.AddSingleton<MainVM>();
            services.AddSingleton<AppSettingVM>();
            services.AddSingleton<BnplSourceVM>();
            services.AddSingleton<BudgetConfigVM>();
            services.AddSingleton<DayOfWeekVM>();
            services.AddSingleton<ExpenseVM>();
            services.AddSingleton<FixedExpenseHistoryVM>();
            services.AddSingleton<FixedExpenseVM>();
            services.AddSingleton<IncomeEntryVM>();
            services.AddSingleton<IncomeSourceVM>();
            services.AddSingleton<SavingsAccountVM>();
            services.AddSingleton<SavingsGoalVM>();
            services.AddSingleton<TagVM>();

            services.AddSingleton<MainWindow>();

            _serviceProvider = services.BuildServiceProvider();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Create and show the main window using the DI container
            var mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}