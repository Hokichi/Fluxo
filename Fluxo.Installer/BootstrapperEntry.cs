using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;
using Fluxo.Installer.Models;
using Fluxo.Installer.Services;
using Fluxo.Installer.ViewModels;
using WixToolset.BootstrapperApplicationApi;

namespace Fluxo.Installer;

internal sealed class InstallerBootstrapperApplication : BootstrapperApplication
{
    private const int SuccessExitCode = 0;
    private const int CancelExitCode = 1602;
    private const int FailureExitCode = 1;
    private const string InstalledExecutableName = "fluxo.exe";
    private const string DefaultInstallFolderName = "fluxo";

    private static readonly string DiagnosticLogPath = Path.Combine(
        Path.GetTempPath(),
        "Fluxo.Installer",
        "bootstrapper-error.log");

    private IBootstrapperCommand? _command;
    private InstallerViewModel? _viewModel;
    private Dispatcher? _uiDispatcher;
    private volatile bool _lastApplyFailed;
    private volatile bool _headlessMode;
    private volatile int _headlessExitCode = FailureExitCode;
    private readonly ManualResetEventSlim _headlessCompleted = new(false);
    private string? _currentBundleVersion;
    private string? _highestDetectedInstalledVersion;
    private string? _registryInstalledVersion;
    private string? _installedExecutableVersion;

    public InstallerBootstrapperApplication()
    {
        DetectBegin += OnDetectBegin;
        DetectRelatedBundle += OnDetectRelatedBundle;
        DetectComplete += OnDetectComplete;
        PlanRelatedBundleType += OnPlanRelatedBundleType;
        PlanRelatedBundle += OnPlanRelatedBundle;
        PlanComplete += OnPlanComplete;
        ApplyComplete += OnApplyComplete;
    }

    protected override void Run()
    {
        var exitCode = ShouldRunInteractiveUi()
            ? RunUiWithStaGuard()
            : RunHeadless();

        engine.Quit(exitCode);
    }

    protected override void OnCreate(CreateEventArgs args)
    {
        base.OnCreate(args);
        _command = args.Command;
    }

    private int RunUiWithStaGuard()
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            return RunUi();
        }

        var exitCode = FailureExitCode;
        var uiThread = new Thread(() => exitCode = RunUi())
        {
            IsBackground = false,
        };
        uiThread.SetApartmentState(ApartmentState.STA);
        uiThread.Start();
        uiThread.Join();
        return exitCode;
    }

    private int RunHeadless()
    {
        _headlessMode = true;
        _headlessExitCode = FailureExitCode;
        _headlessCompleted.Reset();
        _lastApplyFailed = false;

        try
        {
            engine.Detect();
            _headlessCompleted.Wait();
            return _headlessExitCode;
        }
        catch (Exception ex)
        {
            LogFailure(ex);
            return FailureExitCode;
        }
        finally
        {
            _headlessMode = false;
            _headlessCompleted.Reset();
        }
    }

    private bool ShouldRunInteractiveUi()
    {
        // Burn invokes related bundles in quiet/embedded modes during upgrade cleanup.
        // Those runs should stay headless to avoid spawning visible duplicate installer windows.
        return _command is null || _command.Display == Display.Full;
    }

    private int RunUi()
    {
        try
        {
#if DEBUG
            // Pauses execution until you attach a debugger.
            // A dialog will appear showing the process ID.
            System.Diagnostics.Debugger.Launch();
#endif
            var app = new App();
            app.InitializeComponent();
            _uiDispatcher = app.Dispatcher;

            var window = new Views.MainWindow();

            _viewModel = new InstallerViewModel(
                setInstallFolderVariable: value => engine.SetVariableString("InstallFolder", value, formatted: false),
                requestDetect: () =>
                {
                    _lastApplyFailed = false;
                    engine.Detect();
                },
                requestPlan: () => engine.Plan(GetRequestedLaunchAction(), GetRequestedBundleScope()),
                requestApply: () =>
                {
                    try
                    {
                        var parentHandle = new WindowInteropHelper(window).EnsureHandle();
                        engine.Apply(parentHandle);
                    }
                    catch (Exception ex)
                    {
                        LogFailure(ex);
                        _lastApplyFailed = true;
                        DispatchToUi(() => _viewModel?.OnApplyComplete(FailureExitCode));
                    }
                },
                // Burn performs rollback internally before ApplyComplete on apply failures.
                // We report rollback as successful only when the most recent apply failed.
                requestRollback: () => _lastApplyFailed,
                operationMode: GetOperationMode(),
                bundleExecutablePath: GetBundleSourceProcessPath(),
                closeInstallerAction: () =>
                {
                    if (window.Dispatcher.CheckAccess())
                    {
                        window.Close();
                        return;
                    }

                    window.Dispatcher.Invoke(window.Close);
                });

            window.DataContext = _viewModel;
            _viewModel.Begin();

            _ = app.Run(window);
            return _viewModel.ExitCode;
        }
        catch (OperationCanceledException)
        {
            return CancelExitCode;
        }
        catch (Exception ex)
        {
            LogFailure(ex);
            return FailureExitCode;
        }
    }

    private static void LogFailure(Exception exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(DiagnosticLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var content = $"{DateTimeOffset.UtcNow:O}{Environment.NewLine}{exception}{Environment.NewLine}";
            File.AppendAllText(DiagnosticLogPath, content);
        }
        catch
        {
        }
    }

    private void OnDetectBegin(object? sender, DetectBeginEventArgs e)
    {
        _highestDetectedInstalledVersion = null;
        _currentBundleVersion = GetCurrentBundleVersion();
        _registryInstalledVersion = InstalledVersionRegistryReader.ReadInstalledVersion();
        _installedExecutableVersion = GetInstalledExecutableVersion();
    }

    private void OnDetectRelatedBundle(object? sender, DetectRelatedBundleEventArgs e)
    {
        if (!IsInstalledRelatedBundle(e.RelationType) || string.IsNullOrWhiteSpace(e.Version))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_highestDetectedInstalledVersion))
        {
            _highestDetectedInstalledVersion = e.Version;
            return;
        }

        if (TryCompareVersions(e.Version, _highestDetectedInstalledVersion, out var comparison) && comparison > 0)
        {
            _highestDetectedInstalledVersion = e.Version;
        }
    }

    private void OnDetectComplete(object? sender, DetectCompleteEventArgs e)
    {
        var operationMode = GetOperationMode();
        var upToDateDecision = InstallerUpToDateDecision.Evaluate(
            operationMode,
            e.Status,
            _currentBundleVersion,
            _highestDetectedInstalledVersion,
            _registryInstalledVersion,
            _installedExecutableVersion,
            (left, right) => engine.CompareVersions(left, right));

        if (_headlessMode)
        {
            if (upToDateDecision.ShouldSkipInstall)
            {
                _headlessExitCode = SuccessExitCode;
                _headlessCompleted.Set();
                return;
            }

            engine.Plan(GetRequestedLaunchAction(), GetRequestedBundleScope());

            return;
        }

        if (upToDateDecision.ShouldSkipInstall)
        {
            DispatchToUi(() => _viewModel?.OnDetectedUpToDateVersion(
                upToDateDecision.InstalledVersion,
                upToDateDecision.IsNewerVersion));
            return;
        }

        DispatchToUi(() => _viewModel?.OnDetectComplete(e.Status));
    }

    private void OnPlanComplete(object? sender, PlanCompleteEventArgs e)
    {
        if (_headlessMode)
        {
            if (e.Status != 0)
            {
                _headlessExitCode = e.Status;
                _headlessCompleted.Set();
                return;
            }

            engine.Apply(IntPtr.Zero);

            return;
        }

        DispatchToUi(() => _viewModel?.OnPlanComplete(e.Status));
    }

    private static void OnPlanRelatedBundleType(object? sender, PlanRelatedBundleTypeEventArgs e)
    {
        // Prevent Burn from launching cached related bundle executables during upgrade cleanup.
        // Those legacy bundles can display extra installer windows even when parent flow is quiet.
        e.Type = RelatedBundlePlanType.None;
    }

    private static void OnPlanRelatedBundle(object? sender, PlanRelatedBundleEventArgs e)
    {
        // Keep related bundles out of the execution plan to guarantee single visible installer instance.
        e.State = RequestState.None;
    }

    private void OnApplyComplete(object? sender, ApplyCompleteEventArgs e)
    {
        _lastApplyFailed = e.Status != 0;

        if (_headlessMode)
        {
            _headlessExitCode = e.Status;
            _headlessCompleted.Set();
            return;
        }

        DispatchToUi(() => _viewModel?.OnApplyComplete(e.Status));
    }

    private void DispatchToUi(Action callback)
    {
        if (_uiDispatcher is null || _uiDispatcher.CheckAccess())
        {
            callback();
            return;
        }

        _uiDispatcher.Invoke(callback);
    }

    private string? GetCurrentBundleVersion()
    {
        try
        {
            var version = engine.GetVariableVersion("WixBundleVersion");
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }

            version = engine.GetVariableString("WixBundleVersion");
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch
        {
            return null;
        }
    }

    private string? GetInstalledExecutableVersion()
    {
        try
        {
            var installFolder = GetInstallFolderVariable();
            if (string.IsNullOrWhiteSpace(installFolder))
            {
                installFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    DefaultInstallFolderName);
            }

            var executablePath = Path.Combine(installFolder, InstalledExecutableName);
            if (!File.Exists(executablePath))
            {
                return null;
            }

            var versionInfo = FileVersionInfo.GetVersionInfo(executablePath);
            if (!string.IsNullOrWhiteSpace(versionInfo.FileVersion))
            {
                return versionInfo.FileVersion;
            }

            return string.IsNullOrWhiteSpace(versionInfo.ProductVersion)
                ? null
                : versionInfo.ProductVersion;
        }
        catch
        {
            return null;
        }
    }

    private string? GetInstallFolderVariable()
    {
        try
        {
            var installFolder = engine.GetVariableString("InstallFolder");
            return string.IsNullOrWhiteSpace(installFolder) ? null : installFolder;
        }
        catch
        {
            return null;
        }
    }

    private bool TryCompareVersions(string left, string right, out int comparison)
    {
        try
        {
            comparison = engine.CompareVersions(left, right);
            return true;
        }
        catch
        {
            comparison = 0;
            return false;
        }
    }

    private static bool IsInstalledRelatedBundle(RelationType relationType) =>
        relationType == RelationType.Detect
        || relationType == RelationType.Upgrade
        || relationType == RelationType.Update;

    private LaunchAction GetRequestedLaunchAction()
    {
        return GetRequestedOperation() switch
        {
            InstallerRequestedOperation.Uninstall => LaunchAction.Uninstall,
            InstallerRequestedOperation.Repair => LaunchAction.Repair,
            _ => LaunchAction.Install,
        };
    }

    private BundleScope GetRequestedBundleScope()
    {
        return _command?.Scope ?? BundleScope.Default;
    }

    private InstallerOperationMode GetOperationMode()
    {
        return InstallerOperationModeDetector.Detect(
            GetBundleOriginalSourcePath(),
            GetBundleSourceProcessPath(),
            GetCurrentExecutablePath());
    }

    private InstallerRequestedOperation GetRequestedOperation()
    {
        if (_viewModel is not null)
        {
            return _viewModel.RequestedOperation;
        }

        return GetOperationMode() == InstallerOperationMode.Uninstall
            ? InstallerRequestedOperation.Uninstall
            : InstallerRequestedOperation.Install;
    }

    private string GetBundleOriginalSourcePath()
    {
        try
        {
            var sourcePath = engine.GetVariableString("WixBundleOriginalSource");
            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                return sourcePath;
            }
        }
        catch
        {
        }

        return GetCurrentExecutablePath() ?? string.Empty;
    }

    private string GetBundleSourceProcessPath()
    {
        try
        {
            var sourcePath = engine.GetVariableString("WixBundleSourceProcessPath");
            if (!string.IsNullOrWhiteSpace(sourcePath))
            {
                return sourcePath;
            }
        }
        catch
        {
        }

        return GetBundleOriginalSourcePath();
    }

    private static string? GetCurrentExecutablePath()
    {
        try
        {
            return Environment.ProcessPath;
        }
        catch
        {
            return null;
        }
    }
}
