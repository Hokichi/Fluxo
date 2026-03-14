using Fluxo.Data.Context;
using Fluxo.Services.Extensions;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Microsoft.EntityFrameworkCore;

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
            services.RegisterServices(); services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite($"Data Source=fluxo.sql", o => o.MigrationsAssembly("Fluxo.Data")));

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

            using var scope = _serviceProvider!.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();

            // Create and show the main window using the DI container
            var mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}