using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fluxo.Installer.Models;
using Fluxo.Installer.Services;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

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
    private readonly Func<string, bool> fileExists;
    private readonly Action<string> launchInstalledApp;

    public InstallerViewModel(
        IDotNetRuntimeDetector? dotNetRuntimeDetector = null,
        Action<string>? setInstallFolderVariable = null,
        Action? requestDetect = null,
        Action? requestPlan = null,
        Action? requestApply = null,
        Func<string, bool>? fileExists = null,
        Action<string>? launchInstalledApp = null)
    {
        this.dotNetRuntimeDetector = dotNetRuntimeDetector ?? new DotNetRuntimeDetector();
        this.setInstallFolderVariable = setInstallFolderVariable ?? (_ => { });
        this.requestDetect = requestDetect ?? (() => { });
        this.requestPlan = requestPlan ?? (() => { });
        this.requestApply = requestApply ?? (() => { });
        this.fileExists = fileExists ?? File.Exists;
        this.launchInstalledApp = launchInstalledApp ?? LaunchInstalledApp;
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
    private string statusMessage = string.Empty;

    public int ExitCode
    {
        get
        {
            return State switch
            {
                InstallerState.FinishedSuccess => SuccessExitCode,
                InstallerState.FinishedFailed => FailureExitCode,
                _ => CancelExitCode,
            };
        }
    }

    [RelayCommand]
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
        if (!dotNetRuntimeDetector.IsRequiredRuntimeInstalled())
        {
            StatusMessage = ".NET Runtime is required. Install it, then run setup again.";
            return;
        }

        State = InstallerState.Installing;
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

        setInstallFolderVariable(InstallFolder);
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

        setInstallFolderVariable(InstallFolder);
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
        var installedExePath = Path.Combine(InstallFolder, InstalledExecutableName);
        if (fileExists(installedExePath))
        {
            State = InstallerState.FinishedSuccess;
            StatusMessage = "Installation complete.";
            return;
        }

        TransitionToFailure("Verification failed: Fluxo.exe was not found.");
    }

    private void TransitionToFailure(string message)
    {
        State = InstallerState.FinishedFailed;
        StatusMessage = message;
    }

    private bool CanInstall() => State == InstallerState.Welcome && IsValidInstallFolder(InstallFolder);

    [RelayCommand(CanExecute = nameof(CanLaunchApp))]
    private void LaunchApp()
    {
        if (State != InstallerState.FinishedSuccess)
        {
            return;
        }

        var installedExePath = Path.Combine(InstallFolder, InstalledExecutableName);
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
        InstallCommand.NotifyCanExecuteChanged();
        LaunchAppCommand.NotifyCanExecuteChanged();
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