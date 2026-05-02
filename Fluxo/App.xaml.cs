using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Data.Context;
using Fluxo.Data.Extensions;
using Fluxo.Extensions;
using Fluxo.Services.Logging;
using Fluxo.Services.Notifications;
using Fluxo.Services.Dialogs;
using Fluxo.Services.Ui;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.CustomControls;
using Fluxo.Views.Shell;
using Fluxo.Views.Shell.Main;
using Fluxo.Views.Shell.Tray;
using Fluxo.Views.Shell.Wizard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using MainVM = Fluxo.ViewModels.Shell.Main.MainVM;

namespace Fluxo;

/// <summary>
///     Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string StartupTrayArgument = "--startup-tray";
    private const string RestartTrayArgument = "--startup--tray";
    private readonly IDataOperationRunner _dataOperationRunner;
    private readonly IExpenseLogService _expenseLogService;
    private readonly MainVM _mainVM;
    private readonly IStartupNotificationSummaryService _startupNotificationSummaryService;
    private readonly StartupTrayPopupDisplayPolicy _startupTrayPopupDisplayPolicy = new();
    private readonly IUiSettleAwaiter _uiSettleAwaiter;
    private readonly IServiceProvider? _serviceProvider;
    private readonly DispatcherTimer _trayLeftClickTimer;
    private Forms.NotifyIcon? _trayIcon;
    private TrayMenuPopup? _trayMenuPopup;
    private StartupNotificationPopup? _startupNotificationPopup;
    private bool _hasShownStartupTrayPopup;
    private bool _isTrayLeftClickPending;
    private bool _isForcedShutdownRequested;
    private bool _launchInTrayMode;

    public App()
    {
        RegisterGlobalExceptionHandlers();

        var services = new ServiceCollection();
        services
            .AddFluxoData()
            .AddFluxoPresentation()
            .AddUIData();

        _serviceProvider = services.BuildServiceProvider();

        _mainVM = _serviceProvider.GetRequiredService<MainVM>();
        _dataOperationRunner = _serviceProvider.GetRequiredService<IDataOperationRunner>();
        _expenseLogService = _serviceProvider.GetRequiredService<IExpenseLogService>();
        _startupNotificationSummaryService = _serviceProvider.GetRequiredService<IStartupNotificationSummaryService>();
        _uiSettleAwaiter = _serviceProvider.GetRequiredService<IUiSettleAwaiter>();

        _trayLeftClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        _trayLeftClickTimer.Tick += OnTrayLeftClickTimerTick;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _launchInTrayMode = IsTrayLaunchMode(e.Args);
        await InitializeLoggingAsync();

        try
        {
            var loaderPopup = new StartupLoaderPopup();
            bool isFirstRun;

            try
            {
                loaderPopup.Show();
                await _uiSettleAwaiter.WaitForUiReadyAsync(loaderPopup);
                await MigrateDatabaseAsync(_dataOperationRunner);
                await _uiSettleAwaiter.WaitForUiReadyAsync(loaderPopup);
                isFirstRun = await EnsureFirstRunSettingAsync(_dataOperationRunner);
                await _uiSettleAwaiter.WaitForUiReadyAsync(loaderPopup);
                await _expenseLogService.PostTerminationCleanupAsync();
                await _uiSettleAwaiter.WaitForUiReadyAsync(loaderPopup);

                if (!isFirstRun)
                    await _mainVM.InitializeWithStartupStagesAsync(() =>
                        _uiSettleAwaiter.WaitForUiReadyAsync(loaderPopup));
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
            EnsureTrayIconInitialized();

            if (_launchInTrayMode)
            {
                HideMainWindowToTray(mainWindow);
                await TryShowStartupNotificationPopupOnceAsync();
            }
            else
            {
                mainWindow.Show();
            }
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogError(exception, "Unable to start Fluxo.");

            if (_serviceProvider?.GetService<IDialogService>() is { } dialogService)
                dialogService.ShowError(FluxoLogManager.CreateFailureMessage("start Fluxo"), "Fluxo");
            else
                FluxoMessageBox.Show(null, FluxoLogManager.CreateFailureMessage("start Fluxo"), "Fluxo",
                    MessageBoxButton.OK, MessageBoxImage.Error);

            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            DisposeTrayResources();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogFailureForProcess(exception, "dispose tray resources during app shutdown");
        }

        FluxoLogManager.CloseAndFlush();
        base.OnExit(e);
    }

    public async Task RunSetupWizardAsync()
    {
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        MainWindow?.Hide();

        try
        {
            using (var wizardScope = _serviceProvider!.CreateScope())
            {
                var wizard = wizardScope.ServiceProvider.GetRequiredService<QuickSetupWizard>();
                wizard.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                wizard.ShowDialog();
            }

            await _mainVM.Initialize();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogFailureForProcess(exception, "run setup wizard");
            throw;
        }
        finally
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            MainWindow?.Show();
        }
    }

    internal async Task<bool> TryHandleMainWindowClosingToTrayAsync(MainWindow mainWindow)
    {
        if (_isForcedShutdownRequested)
            return false;

        if (!await IsCloseBehaviorMinimizeToTrayAsync())
            return false;

        EnsureTrayIconInitialized();
        HideMainWindowToTray(mainWindow);
        return true;
    }

    private async Task<bool> IsCloseBehaviorMinimizeToTrayAsync()
    {
        try
        {
            var closeBehavior = await _dataOperationRunner.RunAsync("resolve app close behavior", async (scope, ct) =>
            {
                var setting = await scope.UnitOfWork.UserSettings.GetByNameAsync(UserSettingNames.CloseBehavior, ct);
                return UserSettingValueParser.ParseCloseBehavior(setting?.Value, AppCloseBehavior.Exit);
            });

            return closeBehavior == AppCloseBehavior.MinimizeToTray;
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogWarning(
                exception,
                "Unable to resolve close behavior from user settings. Falling back to default exit behavior.");
            return false;
        }
    }

    private static bool IsTrayLaunchMode(string[] args)
    {
        if (args.Length == 0)
            return false;

        return args.Any(arg =>
            string.Equals(arg, StartupTrayArgument, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, RestartTrayArgument, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureTrayIconInitialized()
    {
        if (_trayIcon is not null)
            return;

        var processPath = Environment.ProcessPath;
        var icon = !string.IsNullOrWhiteSpace(processPath)
            ? System.Drawing.Icon.ExtractAssociatedIcon(processPath) ?? System.Drawing.SystemIcons.Application
            : System.Drawing.SystemIcons.Application;

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Fluxo",
            Visible = true
        };
        _trayIcon.MouseClick += OnTrayIconMouseClick;
        _trayIcon.MouseDoubleClick += OnTrayIconMouseDoubleClick;

        EnsureTrayMenuPopupInitialized();
    }

    private void EnsureTrayMenuPopupInitialized()
    {
        if (_trayMenuPopup is not null)
            return;

        _trayMenuPopup = new TrayMenuPopup();
        _trayMenuPopup.OpenFluxoRequested += (_, _) => RestoreMainWindowFromTray();
        _trayMenuPopup.CheckUpdatesRequested += (_, _) => { };
        _trayMenuPopup.RestartFluxoRequested += (_, _) => RestartFromTray();
        _trayMenuPopup.ExitRequested += (_, _) => ExitFromTray();
    }

    private void EnsureStartupNotificationPopupInitialized()
    {
        if (_startupNotificationPopup is not null)
            return;

        _startupNotificationPopup = new StartupNotificationPopup();
        _startupNotificationPopup.OpenAppRequested += OnStartupNotificationPopupOpenAppRequested;
        _startupNotificationPopup.DismissRequested += OnStartupNotificationPopupDismissRequested;
    }

    private async Task TryShowStartupNotificationPopupOnceAsync()
    {
        try
        {
            var summary = await _startupNotificationSummaryService.BuildAsync();
            var shouldShow = _startupTrayPopupDisplayPolicy.ShouldShow(
                _launchInTrayMode,
                _hasShownStartupTrayPopup,
                summary is not null);

            if (!shouldShow || summary is null)
                return;

            if (!IsMainWindowHiddenToTray())
                return;

            EnsureStartupNotificationPopupInitialized();
            if (_startupNotificationPopup is null)
                return;

            var cursorPosition = Forms.Cursor.Position;
            _startupNotificationPopup.SummaryText = summary.Message;
            _startupNotificationPopup.ShowNearScreenPoint(new System.Windows.Point(cursorPosition.X, cursorPosition.Y));
            _hasShownStartupTrayPopup = true;
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogFailureForProcess(exception, "show startup notification popup");
            Debug.WriteLine($"Startup notification popup failed: {exception}");
        }
    }

    private bool IsMainWindowHiddenToTray()
    {
        return MainWindow is MainWindow mainWindow
               && !mainWindow.IsVisible
               && !mainWindow.ShowInTaskbar;
    }

    private void OnStartupNotificationPopupOpenAppRequested(object? sender, EventArgs e)
    {
        RestoreMainWindowFromTray();
    }

    private void OnStartupNotificationPopupDismissRequested(object? sender, EventArgs e)
    {
        _hasShownStartupTrayPopup = true;
    }

    private void OnTrayIconMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        RunOnUiThread(() =>
        {
            if (e.Button == Forms.MouseButtons.Right)
            {
                _trayLeftClickTimer.Stop();
                _isTrayLeftClickPending = false;
                ShowTrayMenuPopup();
                return;
            }

            if (e.Button != Forms.MouseButtons.Left)
                return;

            _isTrayLeftClickPending = true;
            _trayLeftClickTimer.Stop();
            _trayLeftClickTimer.Start();
        });
    }

    private void OnTrayIconMouseDoubleClick(object? sender, Forms.MouseEventArgs e)
    {
        RunOnUiThread(() =>
        {
            if (e.Button != Forms.MouseButtons.Left)
                return;

            _trayLeftClickTimer.Stop();
            _isTrayLeftClickPending = false;
            _trayMenuPopup?.Hide();
            RestoreMainWindowFromTray();
        });
    }

    private void OnTrayLeftClickTimerTick(object? sender, EventArgs e)
    {
        _trayLeftClickTimer.Stop();

        if (!_isTrayLeftClickPending)
            return;

        _isTrayLeftClickPending = false;
        ShowTrayMenuPopup();
    }

    private void ShowTrayMenuPopup()
    {
        EnsureTrayMenuPopupInitialized();
        if (_trayMenuPopup is null)
            return;

        var cursorPosition = Forms.Cursor.Position;
        _trayMenuPopup.ShowNearScreenPoint(new System.Windows.Point(cursorPosition.X, cursorPosition.Y));
    }

    private void HideMainWindowToTray(MainWindow mainWindow)
    {
        _trayMenuPopup?.Hide();
        mainWindow.HideToTray();
    }

    private void RestoreMainWindowFromTray()
    {
        _trayMenuPopup?.Hide();
        _startupNotificationPopup?.Hide();

        if (MainWindow is MainWindow mainWindow)
            mainWindow.ShowFromTray();
    }

    private void RestartFromTray()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath))
                throw new InvalidOperationException("Unable to resolve the current executable path.");

            Process.Start(new ProcessStartInfo
            {
                FileName = processPath,
                Arguments = RestartTrayArgument,
                UseShellExecute = true
            });

            ExitFromTray();
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogFailureForProcess(exception, "restart Fluxo from tray");

            if (_serviceProvider?.GetService<IDialogService>() is { } dialogService)
                dialogService.ShowError(FluxoLogManager.CreateFailureMessage("restart Fluxo"), "Fluxo");
            else
                FluxoMessageBox.Show(null, FluxoLogManager.CreateFailureMessage("restart Fluxo"), "Fluxo",
                    MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExitFromTray()
    {
        _isForcedShutdownRequested = true;
        _trayMenuPopup?.Hide();

        if (MainWindow is MainWindow mainWindow)
            mainWindow.Close();
        else
            Shutdown();
    }

    private void DisposeTrayResources()
    {
        _trayLeftClickTimer.Stop();
        _isTrayLeftClickPending = false;

        if (_trayIcon is not null)
        {
            _trayIcon.MouseClick -= OnTrayIconMouseClick;
            _trayIcon.MouseDoubleClick -= OnTrayIconMouseDoubleClick;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        if (_trayMenuPopup is not null)
        {
            _trayMenuPopup.Close();
            _trayMenuPopup = null;
        }

        if (_startupNotificationPopup is not null)
        {
            _startupNotificationPopup.OpenAppRequested -= OnStartupNotificationPopupOpenAppRequested;
            _startupNotificationPopup.DismissRequested -= OnStartupNotificationPopupDismissRequested;
            _startupNotificationPopup.Close();
            _startupNotificationPopup = null;
        }
    }

    private void RunOnUiThread(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.BeginInvoke(action);
    }

    private static async Task<bool> EnsureFirstRunSettingAsync(IDataOperationRunner dataOperationRunner)
    {
        return await dataOperationRunner.RunAsync("ensure first-run setting", async (scope, ct) =>
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
        return dataOperationRunner.RunAsync("migrate database", async (scope, ct) =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
            await dbContext.Database.MigrateAsync(ct);
        });
    }

    private async Task InitializeLoggingAsync()
    {
        string? username = null;

        try
        {
            username = await _dataOperationRunner.RunAsync("resolve app username for logging", async (scope, ct) =>
            {
                var userSetting = await scope.UnitOfWork.UserSettings
                    .GetByNameAsync(UserSettingNames.PreferredDisplayName, ct);
                return userSetting?.Value;
            });
        }
        catch
        {
            // Falls through to default username initialization.
        }

        try
        {
            FluxoLogManager.Initialize(username);
        }
        catch (Exception exception)
        {
            try
            {
                FluxoLogManager.Initialize("User");
                FluxoLogManager.LogFailureForProcess(
                    exception,
                    "initialize Serilog with the configured app username");
            }
            catch
            {
                // Startup continues even if logging initialization fails.
            }
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        FluxoLogManager.LogError(e.Exception, "Unhandled dispatcher exception.");
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            FluxoLogManager.LogError(
                exception,
                $"Unhandled app domain exception. IsTerminating={e.IsTerminating}");
            return;
        }

        FluxoLogManager.LogInformation(
            $"Unhandled app domain exception object (non-Exception). IsTerminating={e.IsTerminating}");
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        FluxoLogManager.LogError(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }
}
