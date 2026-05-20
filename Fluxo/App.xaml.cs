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
using Fluxo.Services.Updates;
using Fluxo.Infrastructure.SingleInstance;
using Fluxo.ViewModels.Shell;
using Fluxo.Views.Shell;
using Fluxo.Views.Shell.Main;
using Fluxo.Views.Shell.Tray;
using Fluxo.Views.Shell.Wizard;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Data.Common;
using System.Globalization;
using System.IO;
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
    private readonly IStartupRegistrationService _startupRegistrationService;
    private readonly IStartupUpdateNotificationService _startupUpdateNotificationService;
    private readonly IStartupNotificationSummaryService _startupNotificationSummaryService;
    private readonly StartupTrayPopupDisplayPolicy _startupTrayPopupDisplayPolicy = new();
    private readonly IUiSettleAwaiter _uiSettleAwaiter;
    private readonly IServiceProvider _serviceProvider;
    private readonly DispatcherTimer _trayLeftClickTimer;
    private Forms.NotifyIcon? _trayIcon;
    private TrayMenuPopup? _trayMenuPopup;
    private StartupNotificationPopup? _startupNotificationPopup;
    private bool _hasShownStartupTrayPopup;
    private bool _isTrayLeftClickPending;
    private bool _isForcedShutdownRequested;
    private bool _isPrimaryActivationPending;
    private bool _launchInTrayMode;
    private ISingleInstanceCoordinator? _singleInstanceCoordinator;

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
        _startupRegistrationService = _serviceProvider.GetRequiredService<IStartupRegistrationService>();
        _startupUpdateNotificationService = _serviceProvider.GetRequiredService<IStartupUpdateNotificationService>();
        _startupNotificationSummaryService = _serviceProvider.GetRequiredService<IStartupNotificationSummaryService>();
        _uiSettleAwaiter = _serviceProvider.GetRequiredService<IUiSettleAwaiter>();

        _trayLeftClickTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
        _trayLeftClickTimer.Tick += OnTrayLeftClickTimerTick;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        _singleInstanceCoordinator ??= new SingleInstanceCoordinator();
        var shouldContinueStartup = SingleInstanceStartupPolicy.ShouldContinueStartup(
            _singleInstanceCoordinator,
            OnPrimaryActivationRequested);

        if (!shouldContinueStartup)
        {
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _launchInTrayMode = IsTrayLaunchMode(e.Args);

        // Serilog must be ready before IDataOperationRunner runs (it logs failures). Default username until DB exists.
        try
        {
            FluxoLogManager.Initialize("User");
        }
        catch (Exception exception)
        {
            try
            {
                FluxoLogManager.LogFailureForProcess(
                    exception,
                    "bootstrap Serilog before database initialization");
            }
            catch
            {
                // Startup continues; later InitializeLoggingAsync may still configure logging.
            }
        }

        FluxoDbContextFactory.EnsureDatabaseDirectoryExists();
        await BackupDatabaseOnStartupAsync();
        await MigrateDatabaseAsync(_dataOperationRunner);
        await InitializeLoggingAsync();

        try
        {
            var loaderPopup = new StartupLoaderPopup();
            bool isFirstRun;

            try
            {
                loaderPopup.Show();
                await _uiSettleAwaiter.WaitForUiReadyAsync(loaderPopup);
                isFirstRun = await EnsureFirstRunSettingAsync(_dataOperationRunner);
                await _uiSettleAwaiter.WaitForUiReadyAsync(loaderPopup);
                await _expenseLogService.PostTerminationCleanupAsync();
                await _uiSettleAwaiter.WaitForUiReadyAsync(loaderPopup);
                await SyncRunAtStartupRegistrationAsync();
                await _uiSettleAwaiter.WaitForUiReadyAsync(loaderPopup);
                await _startupUpdateNotificationService.CheckAndSyncAsync();
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
            var shouldForegroundOnStartup = _isPrimaryActivationPending;

            if (_launchInTrayMode)
            {
                if (shouldForegroundOnStartup)
                {
                    _isPrimaryActivationPending = false;
                    RestoreMainWindowFromTray();
                }
                else
                {
                    HideMainWindowToTray(mainWindow);
                    await TryShowStartupNotificationPopupOnceAsync();
                }
            }
            else
            {
                if (shouldForegroundOnStartup)
                {
                    _isPrimaryActivationPending = false;
                    RestoreMainWindowFromTray();
                }
                else
                {
                    mainWindow.Show();
                }
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
            _singleInstanceCoordinator?.Dispose();
            _singleInstanceCoordinator = null;
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogFailureForProcess(exception, "dispose app resources during app shutdown");
        }

        FluxoLogManager.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnPrimaryActivationRequested()
    {
        RunOnUiThread(() =>
        {
            if (MainWindow is not Fluxo.Views.Shell.Main.MainWindow)
            {
                _isPrimaryActivationPending = true;
                return;
            }

            _isPrimaryActivationPending = false;
            RestoreMainWindowFromTray();
        });
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

    public void LaunchUpdateInstallerAndShutdown(string installerPath)
    {
        var installFolder = ResolveCurrentInstallFolder();
        var startInfo = AppUpdateInstallerLauncher.CreateStartInfo(installerPath, installFolder);
        var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("The update installer did not start.");
        }

        _isForcedShutdownRequested = true;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Shutdown(0);
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

    private static string ResolveCurrentInstallFolder()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath))
        {
            var directory = Path.GetDirectoryName(processPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        return AppContext.BaseDirectory;
    }

    private static bool IsTrayLaunchMode(string[] args)
    {
        if (args.Length == 0)
            return false;

        return args.Any(arg =>
            string.Equals(arg, StartupTrayArgument, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, RestartTrayArgument, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SyncRunAtStartupRegistrationAsync()
    {
        try
        {
            var shouldRunAtStartup = await _dataOperationRunner.RunAsync(
                "resolve run-at-startup setting",
                async (scope, ct) =>
                {
                    var setting = await scope.UnitOfWork.UserSettings.GetByNameAsync(UserSettingNames.ShouldRunAtStartup, ct);
                    return UserSettingValueParser.ParseBool(setting?.Value, false);
                });

            _startupRegistrationService.SetRunAtStartup(shouldRunAtStartup);
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogWarning(
                exception,
                "Unable to sync Windows startup registration from user settings.");
        }
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

    internal static Task MigrateDatabaseAsync(IDataOperationRunner dataOperationRunner)
    {
        return MigrateDatabaseAsync(dataOperationRunner, FluxoDbContextFactory.GetDatabasePath);
    }

    internal static Task MigrateDatabaseAsync(
        IDataOperationRunner dataOperationRunner,
        Func<string> databasePathProvider)
    {
        return dataOperationRunner.RunAsync("migrate database", async (scope, ct) =>
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FluxoDbContext>();
            var databasePath = databasePathProvider();
            var databaseExists = File.Exists(databasePath);
            var allMigrations = dbContext.Database.GetMigrations().ToList();

            if (!databaseExists)
            {
                await CreateCurrentSchemaAndSeedMigrationHistoryAsync(dbContext, allMigrations, ct);
                return;
            }

            var appliedMigrations = await TryGetAppliedMigrationsAsync(dbContext, ct);

            if (appliedMigrations.Count == 0)
            {
                var hasAnyApplicationTable = await HasAnyApplicationTableAsync(dbContext, ct);
                if (!hasAnyApplicationTable)
                {
                    await CreateCurrentSchemaAndSeedMigrationHistoryAsync(dbContext, allMigrations, ct);
                    return;
                }

                var inferredLatestMigration = await InferLatestAppliedMigrationAsync(dbContext, allMigrations, ct);

                if (!string.IsNullOrWhiteSpace(inferredLatestMigration))
                {
                    await EnsureMigrationHistoryTableAsync(dbContext, ct);
                    await SeedMigrationHistoryAsync(dbContext, allMigrations, inferredLatestMigration, ct);
                    appliedMigrations = await TryGetAppliedMigrationsAsync(dbContext, ct);
                }
            }

            await dbContext.Database.MigrateAsync(ct);
        });
    }

    private static async Task CreateCurrentSchemaAndSeedMigrationHistoryAsync(
        FluxoDbContext dbContext,
        IReadOnlyList<string> allMigrations,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await SeedCurrentSchemaDataAsync(dbContext, cancellationToken);
        await EnsureMigrationHistoryTableAsync(dbContext, cancellationToken);

        if (allMigrations.Count > 0)
            await SeedMigrationHistoryAsync(dbContext, allMigrations, allMigrations[^1], cancellationToken);
    }

    private static async Task SeedCurrentSchemaDataAsync(
        FluxoDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT OR IGNORE INTO "UserSettings" ("Name", "Value")
            VALUES ('AllocationPeriod', 'Monthly');
            """,
            cancellationToken);
    }

    private static async Task<List<string>> TryGetAppliedMigrationsAsync(
        FluxoDbContext dbContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return (await dbContext.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
        }
        catch (SqliteException exception) when (
            exception.SqliteErrorCode == 1 &&
            exception.Message.Contains("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }
    }

    private static async Task EnsureMigrationHistoryTableAsync(
        FluxoDbContext dbContext,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            );
            """,
            cancellationToken);
    }

    private static async Task SeedMigrationHistoryAsync(
        FluxoDbContext dbContext,
        IReadOnlyList<string> allMigrations,
        string latestAppliedMigration,
        CancellationToken cancellationToken)
    {
        var latestIndex = allMigrations
            .ToList()
            .FindIndex(migrationId => string.Equals(migrationId, latestAppliedMigration, StringComparison.Ordinal));
        if (latestIndex < 0)
            return;

        var efCoreVersion = typeof(DbContext).Assembly.GetName().Version;
        var productVersion = efCoreVersion is null
            ? "10.0.0"
            : $"{efCoreVersion.Major}.{efCoreVersion.Minor}.{Math.Max(0, efCoreVersion.Build)}";

        for (var index = 0; index <= latestIndex; index++)
        {
            var migrationId = allMigrations[index];
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT OR IGNORE INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                 VALUES ({migrationId}, {productVersion});
                 """,
                cancellationToken);
        }
    }

    private static async Task<string?> InferLatestAppliedMigrationAsync(
        FluxoDbContext dbContext,
        IReadOnlyList<string> allMigrations,
        CancellationToken cancellationToken)
    {
        if (allMigrations.Count == 0)
            return null;

        var hasCreatedOnColumn = await ColumnExistsAsync(dbContext, "SavingGoals", "CreatedOn", cancellationToken);
        if (hasCreatedOnColumn)
            return "20260421180000_AddSavingGoalCreatedOn";

        var hasMonthlyDueDate = await ColumnExistsAsync(dbContext, "SpendingSources", "MonthlyDueDate", cancellationToken);
        var hasIsSystemTag = await ColumnExistsAsync(dbContext, "ExpenseTags", "IsSystemTag", cancellationToken);
        var hasDeductSource = await ColumnExistsAsync(dbContext, "SpendingSources", "DeductSource", cancellationToken);
        var hasNotificationsTable = await TableExistsAsync(dbContext, "Notifications", cancellationToken);
        var hasIconName = await ColumnExistsAsync(dbContext, "ExpenseTags", "IconName", cancellationToken);

        if (hasMonthlyDueDate && hasIsSystemTag && hasDeductSource && hasNotificationsTable && !hasIconName)
            return "20260421165000_RemoveIconNameFromExpenseTag";

        if (hasMonthlyDueDate && hasIsSystemTag && hasDeductSource && hasNotificationsTable)
        {
            var accountLimitType = await GetColumnTypeAsync(dbContext, "SpendingSources", "AccountLimit", cancellationToken);
            if (string.Equals(accountLimitType, "NUMERIC", StringComparison.OrdinalIgnoreCase))
                return "20260419153000_ConvertMoneyColumnsToNumeric";

            return "20260419120000_AddNotificationsAndDeductSource";
        }

        if (hasMonthlyDueDate && hasIsSystemTag)
            return "20260417064811_AddIsSystemTagToExpenseTag";

        if (hasMonthlyDueDate)
        {
            var monthlyDueDateNullable = await IsColumnNullableAsync(dbContext, "SpendingSources", "MonthlyDueDate", cancellationToken);
            if (monthlyDueDateNullable is true)
                return "20260416170000_MakeMonthlyDueDateNullable";

            return "20260416153000_AddIconNameAndMonthlyDueDateToSpendingSource";
        }

        if (await ColumnExistsAsync(dbContext, "SpendingSources", "IsForDeletion", cancellationToken))
            return "20260415014219_AddIsForDeletionToSpendingSource";

        if (await ColumnExistsAsync(dbContext, "SpendingSources", "IsEnabled", cancellationToken))
            return "20260411142128_AddIsEnabledToSpendingSource";

        if (await ColumnExistsAsync(dbContext, "ExpenseLogs", "IsForDeletion", cancellationToken))
            return "20260408110007_AddIsForDeletionToExpenseLog";

        if (await TableExistsAsync(dbContext, "UserSettings", cancellationToken))
            return "20260408105340_AddUserSettings";

        if (await ColumnExistsAsync(dbContext, "SpendingSources", "ShowOnUI", cancellationToken))
            return "20260401093922_AddShowOnUIToSpendingSource";

        return null;
    }

    private static async Task<bool> HasAnyApplicationTableAsync(
        FluxoDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var tableResult = await ExecuteScalarAsync(
            dbContext,
            """
            SELECT 1
            FROM sqlite_master
            WHERE type = 'table'
              AND name NOT LIKE 'sqlite_%'
              AND name NOT LIKE '__EFMigrations%'
            LIMIT 1;
            """,
            cancellationToken);

        return tableResult is not null;
    }

    private static async Task<bool> TableExistsAsync(
        FluxoDbContext dbContext,
        string tableName,
        CancellationToken cancellationToken)
    {
        var tableResult = await ExecuteScalarAsync(
            dbContext,
            "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;",
            ("$name", tableName),
            cancellationToken);

        return tableResult is not null;
    }

    private static async Task<bool> ColumnExistsAsync(
        FluxoDbContext dbContext,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var tableExists = await TableExistsAsync(dbContext, tableName, cancellationToken);
        if (!tableExists)
            return false;

        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(1))
                continue;

            var name = reader.GetString(1);
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task<string?> GetColumnTypeAsync(
        FluxoDbContext dbContext,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var tableExists = await TableExistsAsync(dbContext, tableName, cancellationToken);
        if (!tableExists)
            return null;

        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(1))
                continue;

            var name = reader.GetString(1);
            if (!string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                continue;

            return reader.IsDBNull(2) ? null : reader.GetString(2);
        }

        return null;
    }

    private static async Task<bool?> IsColumnNullableAsync(
        FluxoDbContext dbContext,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var tableExists = await TableExistsAsync(dbContext, tableName, cancellationToken);
        if (!tableExists)
            return null;

        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (reader.IsDBNull(1))
                continue;

            var name = reader.GetString(1);
            if (!string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
                continue;

            // PRAGMA table_info notnull: 0 means nullable, 1 means not nullable.
            var notNull = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
            return notNull == 0;
        }

        return null;
    }

    private static async Task<object?> ExecuteScalarAsync(
        FluxoDbContext dbContext,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<object?> ExecuteScalarAsync(
        FluxoDbContext dbContext,
        string commandText,
        (string Name, object? Value) parameter,
        CancellationToken cancellationToken)
    {
        await using var connection = dbContext.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        AddParameter(command, parameter.Name, parameter.Value);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
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

    private async Task BackupDatabaseOnStartupAsync()
    {
        var databasePath = FluxoDbContextFactory.GetDatabasePath();
        var databaseDirectoryPath = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(databaseDirectoryPath))
            return;

        var backupDirectoryPath = Path.Combine(databaseDirectoryPath, "backup");

        try
        {
            Directory.CreateDirectory(databaseDirectoryPath);
            Directory.CreateDirectory(backupDirectoryPath);

            if (!File.Exists(databasePath))
                return;

            if (await ShouldSkipDatabaseBackupForFirstRunAsync())
                return;

            var username = await TryResolveBackupUsernameAsync();
            var safeUsername = SanitizeBackupToken(username);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var backupFileName = $"{safeUsername}_{timestamp}_backup.db";
            var backupPath = Path.Combine(backupDirectoryPath, backupFileName);

            File.Copy(databasePath, backupPath, overwrite: true);
            PruneExpiredBackups(backupDirectoryPath);
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogWarning(exception, "Unable to create startup database backup.");
        }
    }

    private async Task<bool> ShouldSkipDatabaseBackupForFirstRunAsync()
    {
        try
        {
            return await _dataOperationRunner.RunAsync("check first-run before database backup", async (scope, ct) =>
            {
                var setting = await scope.UnitOfWork.UserSettings.GetByNameAsync(UserSettingNames.IsFirstRun, ct);
                if (setting is null)
                    return true;

                return !bool.TryParse(setting.Value, out var isFirstRun) || isFirstRun;
            });
        }
        catch (Exception)
        {
            return false;
        }
    }

    private async Task<string?> TryResolveBackupUsernameAsync()
    {
        try
        {
            return await _dataOperationRunner.RunAsync("resolve app username for database backup", async (scope, ct) =>
            {
                var userSetting = await scope.UnitOfWork.UserSettings
                    .GetByNameAsync(UserSettingNames.PreferredDisplayName, ct);
                return userSetting?.Value;
            });
        }
        catch (Exception exception)
        {
            FluxoLogManager.LogWarning(
                exception,
                "Unable to resolve app username for startup database backup. Falling back to default username.");
            return null;
        }
    }

    private static string SanitizeBackupToken(string? username)
    {
        const string defaultUsername = "User";
        var rawUsername = string.IsNullOrWhiteSpace(username) ? defaultUsername : username.Trim();
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitizedCharacters = rawUsername.Where(character => !invalidCharacters.Contains(character)).ToArray();
        var sanitizedUsername = new string(sanitizedCharacters).Trim();
        return string.IsNullOrWhiteSpace(sanitizedUsername) ? defaultUsername : sanitizedUsername;
    }

    private static void PruneExpiredBackups(string backupDirectoryPath)
    {
        var retentionCutoffUtc = DateTime.UtcNow.AddDays(-3);
        var backupFiles = Directory.EnumerateFiles(backupDirectoryPath, "*_backup.db");

        foreach (var backupFile in backupFiles)
        {
            try
            {
                var createdAtUtc = File.GetCreationTimeUtc(backupFile);
                if (createdAtUtc <= retentionCutoffUtc)
                    File.Delete(backupFile);
            }
            catch (Exception exception)
            {
                FluxoLogManager.LogWarning(exception, $"Unable to prune expired backup file: {backupFile}");
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
