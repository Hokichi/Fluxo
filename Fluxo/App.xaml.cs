using AutoMapper;
using Fluxo.Core.Interfaces;
using Fluxo.Data;
using Fluxo.Data.Context;
using Fluxo.Mappings;
using Fluxo.ViewModels.Controls;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Persistence;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Windows;

namespace Fluxo
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private readonly IServiceProvider? _serviceProvider;

        public App()
        {
            // Build the service provider
            var services = new ServiceCollection();
            var mapperConfigurationExpression = new MapperConfigurationExpression();
            mapperConfigurationExpression.AddProfile<EntityViewModelProfile>();
            var mapperConfiguration = new MapperConfiguration(mapperConfigurationExpression, NullLoggerFactory.Instance);

            // Register all services
            services.AddTransient<FluxoDbContext>(_ => new FluxoDbContextFactory().CreateDbContext([]));
            services.AddTransient<IUnitOfWork, UnitOfWork>();
            services.AddSingleton<IMapper>(_ => mapperConfiguration.CreateMapper());
            services.AddTransient<IViewModelReadUnitOfWork<ExpenseVM, ExpenseLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM>, EntityViewModelReadUnitOfWork>();
            services.AddTransient<IViewModelWriteUnitOfWork<ExpenseVM, ExpenseLogVM, ExpenseTagVM, SavingGoalVM, SpendingSourceVM>, EntityViewModelWriteUnitOfWork>();

            // Register all ViewModels
            services.AddSingleton<MainVM>();
            services.AddSingleton<DayOfWeekVM>();

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
