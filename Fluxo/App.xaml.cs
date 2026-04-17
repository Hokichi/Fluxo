using System.Windows;
using Fluxo.Core.Constants;
using Fluxo.Core.Interfaces;
using Fluxo.Data.Extensions;
using Fluxo.Extensions;
using Fluxo.Resources.CustomControls;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Shell;
using Fluxo.Views.Shell.Main;
using Fluxo.Views.Shell.Wizard;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxo;

/// <summary>
///     Interaction logic for App.xaml
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

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var mainVM = _serviceProvider!.GetRequiredService<MainVM>();
            var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();
            var dialogService = _serviceProvider.GetRequiredService<IDialogService>();

            var isFirstRun = await EnsureFirstRunSettingAsync(unitOfWork);

            if (isFirstRun)
            {
                var wizard = dialogService.GetPopupOfType<StartupWizardPopup>();
                wizard.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                wizard.ShowDialog();
            }
            else
            {
                var loaderPopup = new StartupLoaderPopup();
                try
                {
                    loaderPopup.Show();
                    await mainVM.Initialize();
                }
                finally
                {
                    loaderPopup.CloseLoader();
                }
            }

            var mainWindow = _serviceProvider!.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        catch (Exception exception)
        {
            if (_serviceProvider?.GetService<IDialogService>() is { } dialogService)
                dialogService.ShowError($"Unable to start Fluxo.\n\n{exception.Message}", "Fluxo");
            else
                FluxoMessageBox.Show(null, $"Unable to start Fluxo.\n\n{exception.Message}", "Fluxo",
                    MessageBoxButton.OK, MessageBoxImage.Error);

            Shutdown();
        }
    }

    public async Task RunSetupWizardAsync()
    {
        var mainVM = _serviceProvider!.GetRequiredService<MainVM>();
        var unitOfWork = _serviceProvider!.GetRequiredService<IUnitOfWork>();
        var dialogService = _serviceProvider.GetRequiredService<IDialogService>();

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow?.Hide();

        var wizard = dialogService.GetPopupOfType<StartupWizardPopup>();
        wizard.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        wizard.ShowDialog();

        await mainVM.Initialize();

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow?.Show();
    }

    private static async Task<bool> EnsureFirstRunSettingAsync(IUnitOfWork unitOfWork)
    {
        var existingSetting = await unitOfWork.UserSettings.GetByNameAsync(UserSettingNames.IsFirstRun);

        if (existingSetting is null)
        {
            await unitOfWork.UserSettings.AddAsync(new Core.Entities.UserSettings
            {
                Name = UserSettingNames.IsFirstRun,
                Value = bool.TrueString
            });
            await unitOfWork.SaveChangesAsync();
            return true;
        }

        return !bool.TryParse(existingSetting.Value, out var isFirstRun) || isFirstRun;
    }
}
