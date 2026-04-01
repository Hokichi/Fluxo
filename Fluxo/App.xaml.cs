using Fluxo.Data.Extensions;
using Fluxo.Extensions;
using Fluxo.Views.Shell;
using Microsoft.Extensions.DependencyInjection;
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
            var services = new ServiceCollection();
            services
                .AddFluxoData()
                .AddFluxoPresentation()
                .AddUIData();

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
