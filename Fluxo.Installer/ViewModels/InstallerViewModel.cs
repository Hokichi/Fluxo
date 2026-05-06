using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.Installer.Models;
using Fluxo.Installer.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Fluxo.Resources.CustomControls;

namespace Fluxo.Installer.ViewModels;

public partial class InstallerViewModel : ObservableObject
{
    private const int SuccessStatus = 0;
    private const string InstallingChecklistLabel = "Installing";
    private const string RepairingChecklistLabel = "Repairing";
    private const string InstalledExecutableName = "Fluxo.exe";
    private const string RepairerExecutableName = "fluxo.Repairer.exe";
    private const string CleanupScriptPrefix = "fluxo-cleanup-";
    private const int DeferredCleanupRetryCount = 60;
    private const int DeferredCleanupRetryDelaySeconds = 1;
    private const int ProcessTerminationTimeoutMilliseconds = 5000;
    private const int SuccessExitCode = 0;
    private const int CancelExitCode = 1602;
    private const int FailureExitCode = 1;

    private readonly IDotNetRuntimeDetector dotNetRuntimeDetector;
    private readonly Action<string> setInstallFolderVariable;
    private readonly Action requestDetect;
    private readonly Action requestPlan;
    private readonly Action requestApply;
    private readonly Func<bool> requestRollback;
    private readonly Func<bool> requestCancelConfirmation;
    private readonly bool isRollbackConfigured;
    private readonly Func<string, bool> fileExists;
    private readonly Func<string, bool> directoryExists;
    private readonly Func<string, string[]> enumerateFileSystemEntries;
    private readonly Action<string> deleteDirectory;
    private readonly Action<string> deleteFile;
    private readonly Func<string> createDeferredCleanupScriptPath;
    private readonly Action<string, string> writeAllText;
    private readonly Action<ProcessStartInfo> startProcess;
    private readonly Func<IReadOnlyList<int>> getRunningFluxoProcessIds;
    private readonly Func<int, bool> tryTerminateProcessById;
    private readonly Func<InstallerRequestedOperation, bool> requestTerminateRunningAppConfirmation;
    private readonly Action<string> launchInstalledApp;
    private readonly Action<string, string, bool> copyFile;
    private readonly InstallerOperationMode operationMode;
    private readonly string bundleExecutablePath;
    private readonly bool hasConstructorCloseInstallerAction;
    private Action closeInstallerAction = () => { };

    private readonly InstallerChecklistStep prerequisitesChecklistStep = new("Checking prerequisites");
    private readonly InstallerChecklistStep installingChecklistStep = new(InstallingChecklistLabel);
    private readonly InstallerChecklistStep cleanUpChecklistStep = new("Cleaning up");
    private readonly InstallerChecklistStep rollbackChecklistStep = new("Rolling back");

    private bool installFolderExistedBeforeInstall;
    private bool installStarted;
    private string installFolderForCurrentRun = string.Empty;

    public InstallerViewModel(
        IDotNetRuntimeDetector? dotNetRuntimeDetector = null,
        Action<string>? setInstallFolderVariable = null,
        Action? requestDetect = null,
        Action? requestPlan = null,
        Action? requestApply = null,
        Func<string, bool>? fileExists = null,
        Action<string>? launchInstalledApp = null,
        Func<bool>? requestRollback = null,
        Func<bool>? requestCancelConfirmation = null,
        Func<string, bool>? directoryExists = null,
        Func<string, string[]>? enumerateFileSystemEntries = null,
        Action<string>? deleteDirectory = null,
        Action<string>? deleteFile = null,
        Func<string>? createDeferredCleanupScriptPath = null,
        Action<string, string>? writeAllText = null,
        Action<ProcessStartInfo>? startProcess = null,
        Func<IReadOnlyList<int>>? getRunningFluxoProcessIds = null,
        Func<int, bool>? tryTerminateProcessById = null,
        Func<InstallerRequestedOperation, bool>? requestTerminateRunningAppConfirmation = null,
        InstallerOperationMode operationMode = InstallerOperationMode.Install,
        string? bundleExecutablePath = null,
        Action<string, string, bool>? copyFile = null,
        Action? closeInstallerAction = null)
    {
        this.dotNetRuntimeDetector = dotNetRuntimeDetector ?? new DotNetRuntimeDetector();
        this.setInstallFolderVariable = setInstallFolderVariable ?? (_ => { });
        this.requestDetect = requestDetect ?? (() => { });
        this.requestPlan = requestPlan ?? (() => { });
        this.requestApply = requestApply ?? (() => { });
        this.requestRollback = requestRollback ?? (() => false);
        this.requestCancelConfirmation = requestCancelConfirmation ?? ShowCancelConfirmationMessage;
        isRollbackConfigured = requestRollback is not null;
        this.fileExists = fileExists ?? File.Exists;
        this.directoryExists = directoryExists ?? Directory.Exists;
        this.enumerateFileSystemEntries = enumerateFileSystemEntries
            ?? (path => Directory.EnumerateFileSystemEntries(path).ToArray());
        this.deleteDirectory = deleteDirectory ?? (path => Directory.Delete(path, recursive: true));
        this.deleteFile = deleteFile ?? File.Delete;
        this.createDeferredCleanupScriptPath = createDeferredCleanupScriptPath
            ?? (() => Path.Combine(
                Path.GetTempPath(),
                $"{CleanupScriptPrefix}{Guid.NewGuid():N}.cmd"));
        this.writeAllText = writeAllText ?? File.WriteAllText;
        this.startProcess = startProcess ?? (startInfo => _ = Process.Start(startInfo));
        this.getRunningFluxoProcessIds = getRunningFluxoProcessIds ?? GetRunningFluxoProcessIds;
        this.tryTerminateProcessById = tryTerminateProcessById ?? TryTerminateProcessById;
        this.requestTerminateRunningAppConfirmation = requestTerminateRunningAppConfirmation
            ?? ShowTerminateRunningAppConfirmation;
        this.launchInstalledApp = launchInstalledApp ?? LaunchInstalledApp;
        this.operationMode = operationMode;
        this.bundleExecutablePath = bundleExecutablePath ?? string.Empty;
        this.copyFile = copyFile ?? ((source, destination, overwrite) => File.Copy(source, destination, overwrite));
        hasConstructorCloseInstallerAction = closeInstallerAction is not null;
        this.closeInstallerAction = closeInstallerAction ?? (() => { });
        ChecklistSteps = new ObservableCollection<InstallerChecklistStep>(
        [
            prerequisitesChecklistStep,
            installingChecklistStep,
            cleanUpChecklistStep
        ]);
    }

    [ObservableProperty]
    private string title = "fluxo";

    [ObservableProperty]
    private string tagline = "Your Finances All In One Place";

    [ObservableProperty]
    private string installFolder = @"C:\Program Files\fluxo";

    [ObservableProperty]
    private InstallerState state = InstallerState.Welcome;

    [ObservableProperty]
    private InstallerScreen screen = InstallerScreen.Welcome;

    public ObservableCollection<InstallerChecklistStep> ChecklistSteps { get; }

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private InstallerMaintenanceAction selectedMaintenanceAction = InstallerMaintenanceAction.Repair;

    public InstallerRequestedOperation RequestedOperation { get; private set; } = InstallerRequestedOperation.Install;

    public bool IsMaintenanceMode =>
        operationMode == InstallerOperationMode.Maintenance
        || InstallerOperationModeDetector.Detect(bundleExecutablePath, null) == InstallerOperationMode.Maintenance;

    public bool IsUninstallMode => operationMode == InstallerOperationMode.Uninstall;

    public bool IsRepairSelected
    {
        get => SelectedMaintenanceAction == InstallerMaintenanceAction.Repair;
        set
        {
            if (value)
            {
                SelectedMaintenanceAction = InstallerMaintenanceAction.Repair;
            }
        }
    }

    public bool IsUninstallSelected
    {
        get => SelectedMaintenanceAction == InstallerMaintenanceAction.Uninstall;
        set
        {
            if (value)
            {
                SelectedMaintenanceAction = InstallerMaintenanceAction.Uninstall;
            }
        }
    }

    public string FinishedTitle => State switch
    {
        InstallerState.FinishedSuccess => "Let's begin",
        InstallerState.FinishedUpToDate => "Let's begin",
        InstallerState.FinishedUninstalled => "fluxo",
        InstallerState.FinishedCancelled => "Installation cancelled",
        InstallerState.FinishedFailed when RequestedOperation == InstallerRequestedOperation.Uninstall => "Uninstallation failed",
        InstallerState.FinishedFailed when RequestedOperation == InstallerRequestedOperation.Repair => "Repair failed",
        _ => "Installation failed",
    };

    public string FinishedSubtitle => State switch
    {
        InstallerState.FinishedSuccess => "Your finance, simplified",
        InstallerState.FinishedUpToDate => "Version is up-to-date.",
        InstallerState.FinishedUninstalled => "Thank you for letting fluxo help",
        _ => "Please close the setup and run it again",
    };

    public int ExitCode
    {
        get
        {
            return State switch
            {
                InstallerState.FinishedSuccess => SuccessExitCode,
                InstallerState.FinishedUpToDate => SuccessExitCode,
                InstallerState.FinishedUninstalled => SuccessExitCode,
                InstallerState.FinishedFailed => FailureExitCode,
                InstallerState.FinishedCancelled => CancelExitCode,
                _ => CancelExitCode,
            };
        }
    }

    public void Begin()
    {
        if (IsMaintenanceMode)
        {
            StartMaintenance();
            return;
        }

        if (!IsUninstallMode)
        {
            return;
        }

        StartUninstall();
    }

    [RelayCommand(CanExecute = nameof(CanChangeDirectory))]
    private void ChangeDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Install Location",
            InitialDirectory = Directory.Exists(InstallFolder) ? InstallFolder : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        };

        if (dialog.ShowDialog() == true)
        {
            InstallFolder = dialog.FolderName;
            StatusMessage = string.Empty;
            return;
        }

        StatusMessage = "Install location unchanged.";
    }

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private void Install()
    {
        if (IsMaintenanceMode || IsUninstallMode)
        {
            return;
        }

        RequestedOperation = InstallerRequestedOperation.Install;
        Screen = InstallerScreen.Progress;
        State = InstallerState.Installing;
        installStarted = false;
        installFolderExistedBeforeInstall = false;
        installFolderForCurrentRun = string.Empty;
        installingChecklistStep.Label = InstallingChecklistLabel;
        EnsureDefaultChecklistSteps();
        ResetChecklistStates();
        prerequisitesChecklistStep.State = ChecklistStepState.Running;

        if (!IsValidInstallFolder(InstallFolder))
        {
            prerequisitesChecklistStep.State = ChecklistStepState.Failed;
            TransitionToPrerequisitesFailure("A valid install folder is required.");
            return;
        }

        if (!dotNetRuntimeDetector.IsRequiredRuntimeInstalled())
        {
            prerequisitesChecklistStep.State = ChecklistStepState.Failed;
            ShowPrerequisitesFailureMessage();
            TransitionToPrerequisitesFailure(".NET Runtime is required. Install it, then run setup again.");
            return;
        }

        prerequisitesChecklistStep.State = ChecklistStepState.Success;
        if (!EnsureFluxoCanBeStoppedForOperation(InstallerRequestedOperation.Install))
        {
            return;
        }

        installingChecklistStep.State = ChecklistStepState.Running;
        installFolderExistedBeforeInstall = directoryExists(InstallFolder);
        installFolderForCurrentRun = InstallFolder;
        installStarted = true;
        StatusMessage = "Detecting installation state...";
        requestDetect();
    }

    public void OnDetectComplete(int status)
    {
        if (RequestedOperation == InstallerRequestedOperation.Uninstall)
        {
            StatusMessage = "Planning uninstall...";
            requestPlan();
            return;
        }

        if (RequestedOperation == InstallerRequestedOperation.Repair)
        {
            setInstallFolderVariable(GetInstallFolderForCurrentRun());
            StatusMessage = "Planning repair...";
            requestPlan();
            return;
        }

        setInstallFolderVariable(GetInstallFolderForCurrentRun());
        StatusMessage = "Planning installation...";
        requestPlan();
    }

    public void OnDetectedUpToDateVersion()
    {
        prerequisitesChecklistStep.State = ChecklistStepState.Success;
        installingChecklistStep.State = ChecklistStepState.Success;
        cleanUpChecklistStep.State = ChecklistStepState.Success;
        Screen = InstallerScreen.Finished;
        State = InstallerState.FinishedUpToDate;
        StatusMessage = "Detected installed version is up-to-date.";
    }

    public void OnPlanComplete(int status)
    {
        if (status != SuccessStatus)
        {
            TransitionToFailure("Planning failed.");
            return;
        }

        if (RequestedOperation == InstallerRequestedOperation.Uninstall)
        {
            State = InstallerState.Installing;
            StatusMessage = "Uninstalling files...";
            requestApply();
            return;
        }

        if (RequestedOperation == InstallerRequestedOperation.Repair)
        {
            setInstallFolderVariable(GetInstallFolderForCurrentRun());
            State = InstallerState.Installing;
            StatusMessage = "Repairing files...";
            requestApply();
            return;
        }

        setInstallFolderVariable(GetInstallFolderForCurrentRun());
        State = InstallerState.Installing;
        StatusMessage = "Installing files...";
        requestApply();
    }

    public void OnApplyComplete(int status)
    {
        if (status != SuccessStatus)
        {
            TransitionToFailure(RequestedOperation switch
            {
                InstallerRequestedOperation.Uninstall => "Uninstallation failed.",
                InstallerRequestedOperation.Repair => "Repair failed.",
                _ => "Installation failed.",
            });
            return;
        }

        if (RequestedOperation == InstallerRequestedOperation.Uninstall)
        {
            State = InstallerState.Verifying;
            StatusMessage = "Finalizing uninstallation...";
            CompleteUninstall();
            return;
        }

        State = InstallerState.Verifying;
        StatusMessage = "Verifying installation...";
        VerifyInstallation();
    }

    private void VerifyInstallation()
    {
        var installedExePath = Path.Combine(GetInstallFolderForCurrentRun(), InstalledExecutableName);
        if (fileExists(installedExePath))
        {
            if (!EnsureRepairerExecutable(out var repairerError))
            {
                TransitionToFailure(repairerError);
                return;
            }

            installingChecklistStep.State = ChecklistStepState.Success;
            Screen = InstallerScreen.Finished;
            State = InstallerState.FinishedSuccess;
            StatusMessage = RequestedOperation == InstallerRequestedOperation.Repair
                ? "Repair complete."
                : "Installation complete.";
            return;
        }

        TransitionToFailure("Verification failed: Fluxo.exe was not found.");
    }

    private void TransitionToFailure(string message)
    {
        if (installStarted)
        {
            HandlePostStartFailure(message);
            return;
        }

        if (prerequisitesChecklistStep.State == ChecklistStepState.Running)
        {
            prerequisitesChecklistStep.State = ChecklistStepState.Failed;
        }

        Screen = InstallerScreen.Finished;
        State = InstallerState.FinishedFailed;
        StatusMessage = message;
    }

    private void TransitionToPrerequisitesFailure(string message)
    {
        Screen = InstallerScreen.Finished;
        State = InstallerState.FinishedFailed;
        installFolderForCurrentRun = string.Empty;
        StatusMessage = message;
    }

    private bool CanInstall() =>
        !IsMaintenanceMode &&
        !IsUninstallMode &&
        (State == InstallerState.Welcome || State == InstallerState.FinishedFailed) &&
        IsValidInstallFolder(InstallFolder);

    private bool CanChangeDirectory() =>
        !IsMaintenanceMode &&
        !IsUninstallMode &&
        (State == InstallerState.Welcome || State == InstallerState.FinishedFailed);

    [RelayCommand(CanExecute = nameof(CanLaunchApp))]
    private void LaunchApp()
    {
        if (State != InstallerState.FinishedSuccess && State != InstallerState.FinishedUpToDate)
        {
            return;
        }

        try
        {
            var installedExePath = Path.Combine(GetInstallFolderForCurrentRun(), InstalledExecutableName);
            if (!fileExists(installedExePath))
            {
                StatusMessage = "Fluxo executable was not found, closing installer.";
                return;
            }

            launchInstalledApp(installedExePath);
            StatusMessage = "Launching Fluxo...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Launch reported an error: {ex.Message}";
        }
        finally
        {
            closeInstallerAction();
        }
    }

    private bool CanLaunchApp() =>
        RequestedOperation != InstallerRequestedOperation.Uninstall &&
        (State == InstallerState.FinishedSuccess || State == InstallerState.FinishedUpToDate);

    [RelayCommand]
    private void ContinueMaintenance()
    {
        if (!IsMaintenanceMode)
        {
            return;
        }

        if (SelectedMaintenanceAction == InstallerMaintenanceAction.Uninstall)
        {
            RequestedOperation = InstallerRequestedOperation.Uninstall;
            StartUninstall();
            return;
        }

        RequestedOperation = InstallerRequestedOperation.Repair;
        if (!EnsureFluxoCanBeStoppedForOperation(RequestedOperation))
        {
            return;
        }

        installingChecklistStep.Label = RepairingChecklistLabel;
        Screen = InstallerScreen.Progress;
        State = InstallerState.Installing;
        installStarted = true;
        installFolderExistedBeforeInstall = true;
        installFolderForCurrentRun = ResolveInstallFolderForCurrentRun();
        EnsureDefaultChecklistSteps();
        ResetChecklistStates();
        prerequisitesChecklistStep.State = ChecklistStepState.Success;
        installingChecklistStep.State = ChecklistStepState.Running;
        StatusMessage = "Detecting installation state...";
        requestDetect();
    }

    [RelayCommand]
    private void RepairMaintenance()
    {
        if (!IsMaintenanceMode)
        {
            return;
        }

        SelectedMaintenanceAction = InstallerMaintenanceAction.Repair;
        ContinueMaintenance();
    }

    [RelayCommand]
    private void UninstallMaintenance()
    {
        if (!IsMaintenanceMode)
        {
            return;
        }

        SelectedMaintenanceAction = InstallerMaintenanceAction.Uninstall;
        ContinueMaintenance();
    }

    [RelayCommand]
    private void CloseInstaller()
    {
        if (Screen == InstallerScreen.Finished)
        {
            closeInstallerAction();
            return;
        }

        if (!ConfirmCancellation())
        {
            return;
        }

        if (installStarted)
        {
            HandleCancellation();
            return;
        }

        State = InstallerState.FinishedCancelled;
        Screen = InstallerScreen.Finished;
        StatusMessage = "Installation cancelled.";
    }

    private bool ConfirmCancellation()
    {
        try
        {
            return requestCancelConfirmation();
        }
        catch
        {
            return false;
        }
    }

    private static bool ShowCancelConfirmationMessage()
    {
        if (Application.Current is null)
        {
            return false;
        }

        var result = FluxoMessageBox.Show(
            Application.Current.MainWindow,
            "Are you sure you want to cancel the installation?",
            "Cancel installation",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    private static bool ShowTerminateRunningAppConfirmation(InstallerRequestedOperation operation)
    {
        if (Application.Current is null)
        {
            return false;
        }

        var operationText = operation switch
        {
            InstallerRequestedOperation.Repair => "repairing",
            InstallerRequestedOperation.Uninstall => "uninstalling",
            _ => "installing",
        };

        var result = FluxoMessageBox.Show(
            Application.Current.MainWindow,
            $"Fluxo is currently running. Do you want to close it now and continue {operationText}?",
            "Close Fluxo first?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        return result == MessageBoxResult.Yes;
    }

    private void HandleCancellation()
    {
        SetRollbackOnlyChecklist();
        rollbackChecklistStep.State = ChecklistStepState.Running;
        StatusMessage = "Cancelling installation. Starting rollback...";

        var rollbackFailures = ExecuteRollbackAndCleanup();
        if (rollbackFailures.Count == 0)
        {
            rollbackChecklistStep.State = ChecklistStepState.Success;
            StatusMessage = "Installation cancelled. Rollback completed.";
        }
        else
        {
            rollbackChecklistStep.State = ChecklistStepState.Failed;
            StatusMessage = $"Installation cancelled. {string.Join(" ", rollbackFailures)}";
        }

        State = InstallerState.FinishedCancelled;
        Screen = InstallerScreen.Finished;
    }

    private void EnsureDefaultChecklistSteps()
    {
        if (ChecklistSteps.Count == 3
            && ReferenceEquals(ChecklistSteps[0], prerequisitesChecklistStep)
            && ReferenceEquals(ChecklistSteps[1], installingChecklistStep)
            && ReferenceEquals(ChecklistSteps[2], cleanUpChecklistStep))
        {
            return;
        }

        ChecklistSteps.Clear();
        ChecklistSteps.Add(prerequisitesChecklistStep);
        ChecklistSteps.Add(installingChecklistStep);
        ChecklistSteps.Add(cleanUpChecklistStep);
    }

    private void SetRollbackOnlyChecklist()
    {
        if (ChecklistSteps.Count == 1 && ReferenceEquals(ChecklistSteps[0], rollbackChecklistStep))
        {
            return;
        }

        ChecklistSteps.Clear();
        ChecklistSteps.Add(rollbackChecklistStep);
    }

    private List<string> ExecuteRollbackAndCleanup()
    {
        var rollbackFailures = new List<string>();

        if (!isRollbackConfigured)
        {
            rollbackFailures.Add("Rollback unavailable: callback is not configured.");
        }
        else
        {
            try
            {
                if (!requestRollback())
                {
                    rollbackFailures.Add("Rollback failed.");
                }
            }
            catch (Exception ex)
            {
                rollbackFailures.Add($"Rollback failed: {ex.Message}");
            }
        }

        if (!ShouldDeleteInstallFolder())
        {
            cleanUpChecklistStep.State = ChecklistStepState.Success;
            return rollbackFailures;
        }

        cleanUpChecklistStep.State = ChecklistStepState.Running;
        try
        {
            var installFolder = GetInstallFolderForCurrentRun();
            if (!IsSafeDeleteTarget(installFolder))
            {
                throw new InvalidOperationException("Cleanup rejected: install folder path is unsafe for recursive deletion.");
            }

            if (directoryExists(installFolder))
            {
                deleteDirectory(installFolder);
            }

            cleanUpChecklistStep.State = ChecklistStepState.Success;
        }
        catch (Exception ex)
        {
            cleanUpChecklistStep.State = ChecklistStepState.Failed;
            rollbackFailures.Add($"Cleanup failed: {ex.Message}");
        }

        return rollbackFailures;
    }

    public void SetCloseAction(Action? closeAction)
    {
        if (hasConstructorCloseInstallerAction)
        {
            return;
        }

        closeInstallerAction = closeAction ?? (() => { });
    }

    private static void ShowPrerequisitesFailureMessage()
    {
        if (Application.Current is null)
        {
            return;
        }

        _ = MessageBox.Show(
            ".NET Runtime is missing. Please install all required prerequisites, then run this installer again.",
            "Missing prerequisites",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static bool IsValidInstallFolder(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!Path.IsPathRooted(path))
        {
            return false;
        }

        return path.IndexOfAny(Path.GetInvalidPathChars()) < 0;
    }

    partial void OnInstallFolderChanged(string value)
    {
        InstallCommand.NotifyCanExecuteChanged();
    }

    partial void OnStateChanged(InstallerState value)
    {
        ChangeDirectoryCommand.NotifyCanExecuteChanged();
        InstallCommand.NotifyCanExecuteChanged();
        LaunchAppCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(FinishedTitle));
        OnPropertyChanged(nameof(FinishedSubtitle));
    }

    partial void OnSelectedMaintenanceActionChanged(InstallerMaintenanceAction value)
    {
        OnPropertyChanged(nameof(IsRepairSelected));
        OnPropertyChanged(nameof(IsUninstallSelected));
    }

    private void ResetChecklistStates()
    {
        foreach (var checklistStep in ChecklistSteps)
        {
            checklistStep.State = ChecklistStepState.Pending;
        }
    }

    private void HandlePostStartFailure(string message)
    {
        if (installingChecklistStep.State == ChecklistStepState.Running
            || installingChecklistStep.State == ChecklistStepState.Pending)
        {
            installingChecklistStep.State = ChecklistStepState.Failed;
        }

        SetRollbackOnlyChecklist();
        rollbackChecklistStep.State = ChecklistStepState.Running;
        StatusMessage = $"{message} Starting rollback...";

        var rollbackFailures = ExecuteRollbackAndCleanup();

        if (rollbackFailures.Count == 0)
        {
            rollbackChecklistStep.State = ChecklistStepState.Success;
            State = InstallerState.FinishedFailed;
            Screen = InstallerScreen.Finished;
            StatusMessage = $"{message} Rollback completed.";
            return;
        }

        rollbackChecklistStep.State = ChecklistStepState.Failed;
        State = InstallerState.FinishedFailed;
        Screen = InstallerScreen.Finished;
        StatusMessage = $"{message} {string.Join(" ", rollbackFailures)}";
    }

    private bool ShouldDeleteInstallFolder()
    {
        return installStarted && !installFolderExistedBeforeInstall;
    }

    private string GetInstallFolderForCurrentRun()
    {
        return string.IsNullOrWhiteSpace(installFolderForCurrentRun) ? InstallFolder : installFolderForCurrentRun;
    }

    private static bool IsSafeDeleteTarget(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return false;
        }

        var fullPath = Path.GetFullPath(path);
        var rootPath = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return false;
        }

        var normalizedFullPath = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedRootPath = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return !string.Equals(normalizedFullPath, normalizedRootPath, StringComparison.OrdinalIgnoreCase);
    }

    private static void LaunchInstalledApp(string executablePath)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
        };
        _ = Process.Start(startInfo);
    }

    private void StartUninstall()
    {
        RequestedOperation = InstallerRequestedOperation.Uninstall;
        installingChecklistStep.Label = InstallingChecklistLabel;
        if (!EnsureFluxoCanBeStoppedForOperation(RequestedOperation))
        {
            return;
        }

        Screen = InstallerScreen.Uninstall;
        State = InstallerState.Installing;
        installStarted = true;
        installFolderExistedBeforeInstall = true;
        installFolderForCurrentRun = ResolveInstallFolderForCurrentRun();
        EnsureDefaultChecklistSteps();
        ResetChecklistStates();
        prerequisitesChecklistStep.State = ChecklistStepState.Success;
        installingChecklistStep.State = ChecklistStepState.Running;
        StatusMessage = "Detecting installed version...";
        requestDetect();
    }

    private bool EnsureFluxoCanBeStoppedForOperation(InstallerRequestedOperation operation)
    {
        IReadOnlyList<int> runningProcessIds;
        try
        {
            runningProcessIds = getRunningFluxoProcessIds();
        }
        catch
        {
            runningProcessIds = Array.Empty<int>();
        }

        if (runningProcessIds.Count == 0)
        {
            return true;
        }

        if (!requestTerminateRunningAppConfirmation(operation))
        {
            BlockOperationAndFinish(
                operation,
                "is still open",
                operation == InstallerRequestedOperation.Install
                    ? "Please close fluxo and run setup again."
                    : "Please close fluxo and run the repairer again.");
            return false;
        }

        foreach (var processId in runningProcessIds)
        {
            if (!tryTerminateProcessById(processId))
            {
                BlockOperationAndFinish(
                    operation,
                    "could not be terminated",
                    operation == InstallerRequestedOperation.Install
                        ? "Please close fluxo and run setup again."
                        : "Please close fluxo and run the repairer again.");
                return false;
            }
        }

        return true;
    }

    private void BlockOperationAndFinish(
        InstallerRequestedOperation operation,
        string reason,
        string retrySuffix)
    {
        installStarted = false;
        Screen = InstallerScreen.Finished;
        State = InstallerState.FinishedFailed;
        var operationLabel = operation switch
        {
            InstallerRequestedOperation.Repair => "Repair",
            InstallerRequestedOperation.Uninstall => "Uninstallation",
            _ => "Installation",
        };
        StatusMessage = $"{operationLabel} did not run because fluxo {reason}. {retrySuffix}";
    }

    private void CompleteUninstall()
    {
        prerequisitesChecklistStep.State = ChecklistStepState.Success;
        installingChecklistStep.State = ChecklistStepState.Success;
        if (!TryDeleteInstallFolderAfterUninstall(out var cleanupError))
        {
            cleanUpChecklistStep.State = ChecklistStepState.Failed;
            Screen = InstallerScreen.Finished;
            State = InstallerState.FinishedFailed;
            StatusMessage = $"Uninstallation failed: {cleanupError}";
            return;
        }

        cleanUpChecklistStep.State = ChecklistStepState.Success;
        Screen = InstallerScreen.Finished;
        State = InstallerState.FinishedUninstalled;
        StatusMessage = "Uninstallation complete.";
    }

    private void StartMaintenance()
    {
        RequestedOperation = InstallerRequestedOperation.Install;
        SelectedMaintenanceAction = InstallerMaintenanceAction.Repair;
        installingChecklistStep.Label = InstallingChecklistLabel;
        Screen = InstallerScreen.AppFound;
        State = InstallerState.Welcome;
        installStarted = false;
        installFolderExistedBeforeInstall = true;
        installFolderForCurrentRun = ResolveInstallFolderForCurrentRun();
        StatusMessage = string.Empty;
    }

    private bool EnsureRepairerExecutable(out string errorMessage)
    {
        errorMessage = string.Empty;

        var sourcePath = bundleExecutablePath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !fileExists(sourcePath))
        {
            errorMessage = "Installation failed: installer bundle executable was not found.";
            return false;
        }

        try
        {
            var destinationPath = Path.Combine(GetInstallFolderForCurrentRun(), RepairerExecutableName);
            if (string.Equals(
                    Path.GetFullPath(sourcePath),
                    Path.GetFullPath(destinationPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            copyFile(sourcePath, destinationPath, true);
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Installation failed: could not prepare repairer executable. {ex.Message}";
            return false;
        }
    }

    private bool TryDeleteInstallFolderAfterUninstall(out string cleanupError)
    {
        cleanupError = string.Empty;
        var installFolder = GetInstallFolderForCurrentRun();
        if (!IsSafeDeleteTarget(installFolder))
        {
            cleanupError = "cleanup rejected because install folder path is unsafe for recursive deletion.";
            return false;
        }

        if (!directoryExists(installFolder))
        {
            return true;
        }

        try
        {
            DeleteInstallFolderContentsExceptRepairer(installFolder);
            ScheduleDeferredInstallFolderCleanup(installFolder);
            return true;
        }
        catch (Exception ex)
        {
            cleanupError = ex.Message;
            return false;
        }
    }

    private void DeleteInstallFolderContentsExceptRepairer(string installFolder)
    {
        foreach (var entryPath in enumerateFileSystemEntries(installFolder))
        {
            var entryName = Path.GetFileName(entryPath);
            if (string.Equals(entryName, RepairerExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (directoryExists(entryPath))
            {
                deleteDirectory(entryPath);
                continue;
            }

            deleteFile(entryPath);
        }
    }

    private void ScheduleDeferredInstallFolderCleanup(string installFolder)
    {
        var repairerPath = Path.Combine(installFolder, RepairerExecutableName);
        var scriptPath = createDeferredCleanupScriptPath();
        var scriptContent = BuildDeferredCleanupScript(installFolder, repairerPath);
        writeAllText(scriptPath, scriptContent);

        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c \"\"{scriptPath}\"\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        startProcess(startInfo);
    }

    private static string BuildDeferredCleanupScript(string installFolder, string repairerPath)
    {
        static string QuoteForBatchValue(string value) => value.Replace("\"", "\"\"");

        var builder = new StringBuilder();
        builder.AppendLine("@echo off");
        builder.AppendLine("setlocal");
        builder.AppendLine($"set \"INSTALL_DIR={QuoteForBatchValue(installFolder)}\"");
        builder.AppendLine($"set \"REPAIRER_PATH={QuoteForBatchValue(repairerPath)}\"");
        builder.AppendLine($"set /a \"RETRIES={DeferredCleanupRetryCount}\"");
        builder.AppendLine(":wait_loop");
        builder.AppendLine("if not exist \"%REPAIRER_PATH%\" goto delete_folder");
        builder.AppendLine("del /f /q \"%REPAIRER_PATH%\" >nul 2>nul");
        builder.AppendLine("if not exist \"%REPAIRER_PATH%\" goto delete_folder");
        builder.AppendLine("set /a RETRIES-=1");
        builder.AppendLine("if %RETRIES% LEQ 0 goto delete_folder");
        builder.AppendLine($"timeout /t {DeferredCleanupRetryDelaySeconds} /nobreak >nul");
        builder.AppendLine("goto wait_loop");
        builder.AppendLine(":delete_folder");
        builder.AppendLine($"set /a \"RETRIES={DeferredCleanupRetryCount}\"");
        builder.AppendLine(":folder_loop");
        builder.AppendLine("rmdir /s /q \"%INSTALL_DIR%\" >nul 2>nul");
        builder.AppendLine("if not exist \"%INSTALL_DIR%\" goto cleanup_self");
        builder.AppendLine("set /a RETRIES-=1");
        builder.AppendLine("if %RETRIES% LEQ 0 goto cleanup_self");
        builder.AppendLine($"timeout /t {DeferredCleanupRetryDelaySeconds} /nobreak >nul");
        builder.AppendLine("goto folder_loop");
        builder.AppendLine(":cleanup_self");
        builder.AppendLine("del /f /q \"%~f0\" >nul 2>nul");
        builder.AppendLine("exit /b 0");
        return builder.ToString();
    }

    private string ResolveInstallFolderForCurrentRun()
    {
        if (!string.IsNullOrWhiteSpace(installFolderForCurrentRun)
            && IsValidInstallFolder(installFolderForCurrentRun))
        {
            return installFolderForCurrentRun;
        }

        if (!TryResolveInstallFolderFromRepairerPath(out var resolvedFromRepairer))
        {
            return InstallFolder;
        }

        if (IsValidInstallFolder(resolvedFromRepairer))
        {
            InstallFolder = resolvedFromRepairer;
            return resolvedFromRepairer;
        }

        return InstallFolder;
    }

    private bool TryResolveInstallFolderFromRepairerPath(out string resolvedPath)
    {
        resolvedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(bundleExecutablePath))
        {
            return false;
        }

        var executableName = Path.GetFileName(bundleExecutablePath);
        if (!string.Equals(executableName, RepairerExecutableName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var parentDirectory = Path.GetDirectoryName(bundleExecutablePath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            return false;
        }

        resolvedPath = parentDirectory;
        return true;
    }

    private static IReadOnlyList<int> GetRunningFluxoProcessIds()
    {
        var processIds = new List<int>();
        foreach (var process in Process.GetProcessesByName("fluxo"))
        {
            try
            {
                processIds.Add(process.Id);
            }
            finally
            {
                process.Dispose();
            }
        }

        return processIds;
    }

    private static bool TryTerminateProcessById(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (process.HasExited)
            {
                return true;
            }

            process.Kill(entireProcessTree: true);
            return process.WaitForExit(ProcessTerminationTimeoutMilliseconds);
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }
}
