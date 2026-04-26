using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Data.Context;
using Fluxo.Data.Extensions;
using Fluxo.Extensions;
using Fluxo.Services.Dialogs;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.CustomControls;
using Fluxo.Views.Shell;
using Fluxo.Views.Shell.Main;
using Fluxo.Views.Shell.Wizard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly MainVM _mainVM;
    private readonly IServiceProvider? _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        services
            .AddFluxoData()
            .AddFluxoPresentation()
            .AddUIData();

        _serviceProvider = services.BuildServiceProvider();

        _mainVM = _serviceProvider.GetRequiredService<MainVM>();
        _dataOperationRunner = _serviceProvider.GetRequiredService<IDataOperationRunner>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var loaderPopup = new StartupLoaderPopup();
            bool isFirstRun;

            try
            {
                loaderPopup.Show();
                await MigrateDatabaseAsync(_dataOperationRunner);
                isFirstRun = await EnsureFirstRunSettingAsync(_dataOperationRunner);

                if (!isFirstRun)
                    await _mainVM.Initialize();
            }
            finally
            {
                loaderPopup.CloseLoader();
            }

            if (isFirstRun)
            {
                using var wizardScope = _serviceProvider!.CreateScope();
                var wizard = wizardScope.ServiceProvider.GetRequiredService<QuickSetupWizard>();
                wizard.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                wizard.ShowDialog();
            }

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
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
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow?.Hide();

        using (var wizardScope = _serviceProvider!.CreateScope())
        {
            var wizard = wizardScope.ServiceProvider.GetRequiredService<QuickSetupWizard>();
            wizard.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            wizard.ShowDialog();
        }

        await _mainVM.Initialize();

        ShutdownMode = ShutdownMode.OnMainWindowClose;
        MainWindow?.Show();
    }

    private static async Task<bool> EnsureFirstRunSettingAsync(IDataOperationRunner dataOperationRunner)
    {
        return await dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var existingSetting = await scope.UnitOfWork.UserSettings.GetByNameAsync(UserSettingNames.IsFirstRun, ct);

            if (existingSetting is null)
            {
                await scope.UnitOfWork.UserSettings.AddAsync(new UserSettings
                {
                    Name = UserSettingNames.IsFirstRun,
                    Value = bool.TrueString
                }, ct);
                await scope.UnitOfWork.SaveChangesAsync(ct);
                return true;
            }

            return !bool.TryParse(existingSetting.Value, out var isFirstRun) || isFirstRun;
        });
    }

    private static Task MigrateDatabaseAsync(IDataOperationRunner dataOperationRunner)
    {
        return dataOperationRunner.RunAsync(async (scope, ct) =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
            await dbContext.Database.MigrateAsync(ct);
        });
    }
}
