using System.IO;
using System.Threading;
using System.Windows.Interop;
using System.Windows.Threading;
using Fluxo.Installer.ViewModels;
using WixToolset.BootstrapperApplicationApi;

namespace Fluxo.Installer;

internal sealed class InstallerBootstrapperApplication : BootstrapperApplication
{
    private const int CancelExitCode = 1602;
    private const int FailureExitCode = 1;

    private static readonly string DiagnosticLogPath = Path.Combine(
        Path.GetTempPath(),
        "Fluxo.Installer",
        "bootstrapper-error.log");

    private InstallerViewModel? _viewModel;
    private Dispatcher? _uiDispatcher;
    private volatile bool _lastApplyFailed;

    public InstallerBootstrapperApplication()
    {
        DetectComplete += OnDetectComplete;
        PlanComplete += OnPlanComplete;
        ApplyComplete += OnApplyComplete;
    }

    protected override void Run()
    {
        var exitCode = RunUiWithStaGuard();
        engine.Quit(exitCode);
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
                requestPlan: () => engine.Plan(LaunchAction.Install, BundleScope.Default),
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

    private void OnDetectComplete(object? sender, DetectCompleteEventArgs e)
    {
        DispatchToUi(() => _viewModel?.OnDetectComplete(e.Status));
    }

    private void OnPlanComplete(object? sender, PlanCompleteEventArgs e)
    {
        DispatchToUi(() => _viewModel?.OnPlanComplete(e.Status));
    }

    private void OnApplyComplete(object? sender, ApplyCompleteEventArgs e)
    {
        _lastApplyFailed = e.Status != 0;
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
}
