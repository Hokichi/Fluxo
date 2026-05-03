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
using System.Windows;

namespace Fluxo.Installer.ViewModels;

public partial class InstallerViewModel : ObservableObject
{
    private const int SuccessStatus = 0;
    private const string InstalledExecutableName = "Fluxo.exe";
    private const int SuccessExitCode = 0;
    private const int CancelExitCode = 1602;
    private const int FailureExitCode = 1;

    private readonly IDotNetRuntimeDetector dotNetRuntimeDetector;
    private readonly Action<string> setInstallFolderVariable;
    private readonly Action requestDetect;
    private readonly Action requestPlan;
    private readonly Action requestApply;
    private readonly Func<bool> requestRollback;
    private readonly bool isRollbackConfigured;
    private readonly Func<string, bool> fileExists;
    private readonly Func<string, bool> directoryExists;
    private readonly Action<string> deleteDirectory;
    private readonly Action<string> launchInstalledApp;
    private readonly bool hasConstructorCloseInstallerAction;
    private Action closeInstallerAction = () => { };

    private readonly InstallerChecklistStep prerequisitesChecklistStep = new("Prerequisites");
    private readonly InstallerChecklistStep installingChecklistStep = new("Installing the app");
    private readonly InstallerChecklistStep cleanUpChecklistStep = new("Clean up (if exists)");
    private readonly InstallerChecklistStep rollbackChecklistStep = new("Rollback (if failed)");

    private bool installFolderExistedBeforeInstall;
    private bool installStarted;
    private bool hasUnresolvedPrerequisitesFailure;
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
        Func<string, bool>? directoryExists = null,
        Action<string>? deleteDirectory = null,
        Action? closeInstallerAction = null)
    {
        this.dotNetRuntimeDetector = dotNetRuntimeDetector ?? new DotNetRuntimeDetector();
        this.setInstallFolderVariable = setInstallFolderVariable ?? (_ => { });
        this.requestDetect = requestDetect ?? (() => { });
        this.requestPlan = requestPlan ?? (() => { });
        this.requestApply = requestApply ?? (() => { });
        this.requestRollback = requestRollback ?? (() => false);
        isRollbackConfigured = requestRollback is not null;
        this.fileExists = fileExists ?? File.Exists;
        this.directoryExists = directoryExists ?? Directory.Exists;
        this.deleteDirectory = deleteDirectory ?? (path => Directory.Delete(path, recursive: true));
        this.launchInstalledApp = launchInstalledApp ?? LaunchInstalledApp;
        hasConstructorCloseInstallerAction = closeInstallerAction is not null;
        this.closeInstallerAction = closeInstallerAction ?? (() => { });
        ChecklistSteps = new ObservableCollection<InstallerChecklistStep>(
        [
            prerequisitesChecklistStep,
            installingChecklistStep,
            cleanUpChecklistStep,
            rollbackChecklistStep,
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

    public int ExitCode
    {
        get
        {
            if (State == InstallerState.Welcome && hasUnresolvedPrerequisitesFailure)
            {
                return FailureExitCode;
            }

            return State switch
            {
                InstallerState.FinishedSuccess => SuccessExitCode,
                InstallerState.FinishedFailed => FailureExitCode,
                _ => CancelExitCode,
            };
        }
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
        Screen = InstallerScreen.Progress;
        State = InstallerState.Installing;
        hasUnresolvedPrerequisitesFailure = false;
        installStarted = false;
        installFolderExistedBeforeInstall = false;
        installFolderForCurrentRun = string.Empty;
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
        installingChecklistStep.State = ChecklistStepState.Running;
        installFolderExistedBeforeInstall = directoryExists(InstallFolder);
        installFolderForCurrentRun = InstallFolder;
        installStarted = true;
        StatusMessage = "Detecting installation state...";
        requestDetect();
    }

    public void OnDetectComplete(int status)
    {
        if (status != SuccessStatus)
        {
            TransitionToFailure("Detection failed.");
            return;
        }

        setInstallFolderVariable(GetInstallFolderForCurrentRun());
        StatusMessage = "Planning installation...";
        requestPlan();
    }

    public void OnPlanComplete(int status)
    {
        if (status != SuccessStatus)
        {
            TransitionToFailure("Planning failed.");
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
            TransitionToFailure("Installation failed.");
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
            installingChecklistStep.State = ChecklistStepState.Success;
            Screen = InstallerScreen.Finished;
            State = InstallerState.FinishedSuccess;
            StatusMessage = "Installation complete.";
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

        Screen = InstallerScreen.Progress;
        State = InstallerState.FinishedFailed;
        StatusMessage = message;
    }

    private void TransitionToPrerequisitesFailure(string message)
    {
        Screen = InstallerScreen.Progress;
        State = InstallerState.Welcome;
        hasUnresolvedPrerequisitesFailure = true;
        installFolderForCurrentRun = string.Empty;
        StatusMessage = message;
    }

    private bool CanInstall() =>
        (State == InstallerState.Welcome || State == InstallerState.FinishedFailed) &&
        IsValidInstallFolder(InstallFolder);

    private bool CanChangeDirectory() =>
        State == InstallerState.Welcome || State == InstallerState.FinishedFailed;

    [RelayCommand(CanExecute = nameof(CanLaunchApp))]
    private void LaunchApp()
    {
        if (State != InstallerState.FinishedSuccess)
        {
            return;
        }

        var installedExePath = Path.Combine(GetInstallFolderForCurrentRun(), InstalledExecutableName);
        if (!fileExists(installedExePath))
        {
            TransitionToFailure("Launch failed: Fluxo.exe was not found.");
            return;
        }

        try
        {
            launchInstalledApp(installedExePath);
            StatusMessage = "Launching Fluxo...";
        }
        catch (Exception ex)
        {
            TransitionToFailure($"Launch failed: {ex.Message}");
        }
    }

    private bool CanLaunchApp() => State == InstallerState.FinishedSuccess;

    [RelayCommand]
    private void CloseInstaller()
    {
        closeInstallerAction();
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

        rollbackChecklistStep.State = ChecklistStepState.Running;
        StatusMessage = $"{message} Starting rollback...";

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

        if (ShouldDeleteInstallFolder())
        {
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
        }
        else
        {
            cleanUpChecklistStep.State = ChecklistStepState.Success;
        }

        if (rollbackFailures.Count == 0)
        {
            rollbackChecklistStep.State = ChecklistStepState.Success;
            Screen = InstallerScreen.Progress;
            State = InstallerState.FinishedFailed;
            StatusMessage = $"{message} Rollback completed.";
            return;
        }

        rollbackChecklistStep.State = ChecklistStepState.Failed;
        Screen = InstallerScreen.Progress;
        State = InstallerState.FinishedFailed;
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
}
