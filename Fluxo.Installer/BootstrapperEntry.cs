using WixToolset.Mba.Core;
using System.IO;
using System.Threading;
using Fluxo.Installer.ViewModels;
using System.Windows.Threading;

[assembly: BootstrapperApplicationFactory(typeof(Fluxo.Installer.InstallerFactory))]

namespace Fluxo.Installer;

public sealed class InstallerFactory : BaseBootstrapperApplicationFactory
{
    protected override BootstrapperApplication Create(IEngine engine, IBootstrapperCommand bootstrapperCommand)
    {
        return new InstallerBootstrapperApplication(engine, bootstrapperCommand);
    }
}

internal sealed class InstallerBootstrapperApplication : BootstrapperApplication
{
    private const int SuccessExitCode = 0;
    private const int CancelExitCode = 1602;
    private const int FailureExitCode = 1;

    private static readonly string DiagnosticLogPath = Path.Combine(
        Path.GetTempPath(),
        "Fluxo.Installer",
        "bootstrapper-error.log");

    private readonly IEngine _engine;
    private InstallerViewModel? _viewModel;
    private Dispatcher? _uiDispatcher;
    private volatile bool lastApplyFailed;

    public InstallerBootstrapperApplication(IEngine engine, IBootstrapperCommand bootstrapperCommand)
        : base(engine)
    {
        _engine = engine;
        _ = bootstrapperCommand;
        DetectComplete += OnDetectComplete;
        PlanComplete += OnPlanComplete;
        ApplyComplete += OnApplyComplete;
    }

    protected override void Run()
    {
        var exitCode = RunUiWithStaGuard();
        _engine.Quit(exitCode);
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
                setInstallFolderVariable: value => _engine.SetVariableString("InstallFolder", value, formatted: false),
                requestDetect: () =>
                {
                    lastApplyFailed = false;
                    _engine.Detect();
                },
                requestPlan: () => _engine.Plan(LaunchAction.Install),
                requestApply: () => _engine.Apply(IntPtr.Zero),
                // Burn performs rollback internally before ApplyComplete on apply failures.
                // We report rollback as successful only when the most recent apply failed.
                requestRollback: () => lastApplyFailed,
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
        lastApplyFailed = e.Status != 0;
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