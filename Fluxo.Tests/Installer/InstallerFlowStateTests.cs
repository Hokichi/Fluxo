using Fluxo.Installer.Models;
using Fluxo.Installer.Services;
using Fluxo.Installer.ViewModels;
using System;
using System.Collections.Generic;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class InstallerFlowStateTests
{
    [Fact]
    public void InstallCommand_TriggersDetect_AndMovesToInstalling()
    {
        var detectCalls = 0;
        var vm = CreateViewModel(
            requestDetect: () => detectCalls++,
            fileExists: static _ => true);

        vm.InstallCommand.Execute(null);

        Assert.Equal(InstallerState.Installing, vm.State);
        Assert.Equal("Detecting installation state...", vm.StatusMessage);
        Assert.Equal(1, detectCalls);
    }

    [Fact]
    public void PlanComplete_Success_TransitionsToApplying()
    {
        var applyCalls = 0;
        var folderVariableValues = new List<string>();
        var vm = CreateViewModel(
            setInstallFolderVariable: value => folderVariableValues.Add(value),
            requestApply: () => applyCalls++,
            fileExists: static _ => true);

        vm.OnPlanComplete(0);

        Assert.Equal(InstallerState.Installing, vm.State);
        Assert.Equal("Installing files...", vm.StatusMessage);
        Assert.Single(folderVariableValues);
        Assert.Equal(vm.InstallFolder, folderVariableValues[0]);
        Assert.Equal(1, applyCalls);
    }

    [Fact]
    public void DetectComplete_Success_RequestsPlan_AndKeepsInstallingState()
    {
        var detectCalls = 0;
        var planCalls = 0;
        var folderVariableValues = new List<string>();
        var vm = CreateViewModel(
            setInstallFolderVariable: value => folderVariableValues.Add(value),
            requestDetect: () => detectCalls++,
            requestPlan: () => planCalls++,
            fileExists: static _ => true);

        vm.InstallCommand.Execute(null);
        vm.OnDetectComplete(0);

        Assert.Equal(InstallerState.Installing, vm.State);
        Assert.Equal("Planning installation...", vm.StatusMessage);
        Assert.Single(folderVariableValues);
        Assert.Equal(vm.InstallFolder, folderVariableValues[0]);
        Assert.Equal(1, detectCalls);
        Assert.Equal(1, planCalls);
    }

    [Fact]
    public void DetectComplete_Failure_ContinuesToPlanning()
    {
        var planCalls = 0;
        var folderVariableValues = new List<string>();
        var vm = CreateViewModel(
            setInstallFolderVariable: value => folderVariableValues.Add(value),
            requestPlan: () => planCalls++,
            fileExists: static _ => true);

        vm.OnDetectComplete(1);

        Assert.Equal(InstallerState.Welcome, vm.State);
        Assert.Equal("Planning installation...", vm.StatusMessage);
        Assert.Single(folderVariableValues);
        Assert.Equal(vm.InstallFolder, folderVariableValues[0]);
        Assert.Equal(1, planCalls);
    }

    [Fact]
    public void CloseInstaller_OnWelcome_DeclineCancellation_KeepsInstallerOpen()
    {
        var closeCalls = 0;
        var vm = CreateViewModel(
            requestCancelConfirmation: () => false,
            closeInstallerAction: () => closeCalls++);

        vm.CloseInstallerCommand.Execute(null);

        Assert.Equal(0, closeCalls);
        Assert.Equal(InstallerState.Welcome, vm.State);
        Assert.Equal(InstallerScreen.Welcome, vm.Screen);
    }

    [Fact]
    public void CloseInstaller_OnWelcome_ConfirmCancellation_TransitionsToFinishedCancelled()
    {
        var closeCalls = 0;
        var vm = CreateViewModel(
            requestCancelConfirmation: () => true,
            closeInstallerAction: () => closeCalls++);

        vm.CloseInstallerCommand.Execute(null);

        Assert.Equal(0, closeCalls);
        Assert.Equal(InstallerState.FinishedCancelled, vm.State);
        Assert.Equal(InstallerScreen.Finished, vm.Screen);
        Assert.Equal("Installation cancelled.", vm.StatusMessage);
        Assert.Equal("Installation cancelled", vm.FinishedTitle);
        Assert.Equal("Please close the setup and run it again", vm.FinishedSubtitle);
    }

    [Fact]
    public void CloseInstaller_DuringInstall_ConfirmCancellation_RunsRollback_AndShowsRollbackChecklistOnly()
    {
        var rollbackCalls = 0;
        var vm = CreateViewModel(
            requestRollback: () =>
            {
                rollbackCalls++;
                return true;
            },
            requestCancelConfirmation: () => true,
            requestDetect: static () => { },
            fileExists: static _ => true);

        vm.InstallCommand.Execute(null);
        vm.CloseInstallerCommand.Execute(null);

        Assert.Equal(1, rollbackCalls);
        Assert.Equal(InstallerState.FinishedCancelled, vm.State);
        Assert.Equal(InstallerScreen.Finished, vm.Screen);
        Assert.Single(vm.ChecklistSteps);
        Assert.Equal("Rolling back", vm.ChecklistSteps[0].Label);
        Assert.Equal(ChecklistStepState.Success, vm.ChecklistSteps[0].State);
        Assert.Equal("Installation cancelled", vm.FinishedTitle);
    }

    [Fact]
    public void DetectComplete_Failure_AfterInstallStart_ContinuesToPlanning()
    {
        var planCalls = 0;
        var vm = CreateViewModel(
            requestPlan: () => planCalls++,
            requestDetect: static () => { },
            fileExists: static _ => true);

        vm.InstallCommand.Execute(null);
        vm.OnDetectComplete(1);

        Assert.Equal(1, planCalls);
        Assert.Equal(InstallerState.Installing, vm.State);
        Assert.Equal(InstallerScreen.Progress, vm.Screen);
        Assert.Equal("Planning installation...", vm.StatusMessage);
    }

    [Fact]
    public void DetectedUpToDateVersion_TransitionsToFinishedPage_WithExpectedSubtitle()
    {
        var vm = CreateViewModel(fileExists: static _ => true);

        vm.OnDetectedUpToDateVersion();

        Assert.Equal(InstallerState.FinishedUpToDate, vm.State);
        Assert.Equal(InstallerScreen.Finished, vm.Screen);
        Assert.Equal("Let's begin", vm.FinishedTitle);
        Assert.Equal("Version is up-to-date.", vm.FinishedSubtitle);
        Assert.Equal(0, vm.ExitCode);
    }

    [Fact]
    public void ApplyComplete_Success_TransitionsToVerifying_ThenSuccess()
    {
        var observedStates = new List<InstallerState>();
        var vm = CreateViewModel(fileExists: static _ => true);
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InstallerViewModel.State))
            {
                observedStates.Add(vm.State);
            }
        };

        vm.OnApplyComplete(0);

        Assert.Contains(InstallerState.Verifying, observedStates);
        Assert.Equal(InstallerState.FinishedSuccess, vm.State);
        Assert.Equal("Installation complete.", vm.StatusMessage);
        Assert.Equal(0, vm.ExitCode);
    }

    [Fact]
    public void Begin_UninstallMode_SkipsWelcome_AndStartsDetection()
    {
        var detectCalls = 0;
        var vm = CreateViewModel(
            requestDetect: () => detectCalls++,
            operationMode: InstallerOperationMode.Uninstall);

        vm.Begin();

        Assert.Equal(InstallerScreen.Uninstall, vm.Screen);
        Assert.Equal(InstallerState.Installing, vm.State);
        Assert.Equal("Detecting installed version...", vm.StatusMessage);
        Assert.Equal(1, detectCalls);
    }

    [Fact]
    public void Begin_RepairerBundlePath_SkipsWelcome_EvenWhenOperationModeDefaultsToInstall()
    {
        var vm = new InstallerViewModel(
            dotNetRuntimeDetector: new FixedRuntimeDetector(true),
            bundleExecutablePath: @"G:\fluxo\fluxo.Repairer.exe",
            copyFile: static (_, _, _) => { });

        vm.Begin();

        Assert.True(vm.IsMaintenanceMode);
        Assert.False(vm.IsUninstallMode);
        Assert.Equal(InstallerScreen.AppFound, vm.Screen);
        Assert.Equal(InstallerState.Welcome, vm.State);
    }

    [Fact]
    public void Begin_MaintenanceMode_ShowsAppFoundPage()
    {
        var vm = CreateViewModel(operationMode: InstallerOperationMode.Maintenance);

        vm.Begin();

        Assert.Equal(InstallerScreen.AppFound, vm.Screen);
        Assert.Equal(InstallerState.Welcome, vm.State);
        Assert.Equal(InstallerMaintenanceAction.Repair, vm.SelectedMaintenanceAction);
        Assert.Equal(InstallerRequestedOperation.Install, vm.RequestedOperation);
    }

    [Fact]
    public void Begin_RepairerBundlePath_ShowsAppFoundPage()
    {
        var vm = new InstallerViewModel(
            dotNetRuntimeDetector: new FixedRuntimeDetector(true),
            bundleExecutablePath: @"G:\fluxo\fluxo.Repairer.exe",
            copyFile: static (_, _, _) => { });

        vm.Begin();

        Assert.Equal(InstallerScreen.AppFound, vm.Screen);
        Assert.False(vm.IsUninstallMode);
        Assert.True(vm.IsMaintenanceMode);
    }

    [Fact]
    public void ContinueMaintenance_DefaultRepair_RequestsDetectAndSetsRepairOperation()
    {
        var detectCalls = 0;
        var vm = CreateViewModel(
            requestDetect: () => detectCalls++,
            operationMode: InstallerOperationMode.Maintenance);

        vm.Begin();
        vm.ContinueMaintenanceCommand.Execute(null);

        Assert.Equal(InstallerRequestedOperation.Repair, vm.RequestedOperation);
        Assert.Equal(InstallerScreen.Progress, vm.Screen);
        Assert.Equal("Detecting installation state...", vm.StatusMessage);
        Assert.Equal(1, detectCalls);
    }

    [Fact]
    public void ContinueMaintenance_Uninstall_RequestsDetectAndSetsUninstallOperation()
    {
        var detectCalls = 0;
        var vm = CreateViewModel(
            requestDetect: () => detectCalls++,
            operationMode: InstallerOperationMode.Maintenance);

        vm.Begin();
        vm.SelectedMaintenanceAction = InstallerMaintenanceAction.Uninstall;
        vm.ContinueMaintenanceCommand.Execute(null);

        Assert.Equal(InstallerRequestedOperation.Uninstall, vm.RequestedOperation);
        Assert.Equal(InstallerScreen.Uninstall, vm.Screen);
        Assert.Equal("Detecting installed version...", vm.StatusMessage);
        Assert.Equal(1, detectCalls);
    }

    [Fact]
    public void DetectComplete_RepairOperation_RequestsPlan()
    {
        var planCalls = 0;
        var vm = CreateViewModel(
            requestPlan: () => planCalls++,
            operationMode: InstallerOperationMode.Maintenance);

        vm.Begin();
        vm.ContinueMaintenanceCommand.Execute(null);
        vm.OnDetectComplete(0);

        Assert.Equal(InstallerRequestedOperation.Repair, vm.RequestedOperation);
        Assert.Equal("Planning repair...", vm.StatusMessage);
        Assert.Equal(1, planCalls);
    }

    [Fact]
    public void DetectComplete_UninstallMode_RequestsPlan()
    {
        var planCalls = 0;
        var vm = CreateViewModel(
            requestPlan: () => planCalls++,
            operationMode: InstallerOperationMode.Uninstall);

        vm.Begin();
        vm.OnDetectComplete(0);

        Assert.Equal("Planning uninstall...", vm.StatusMessage);
        Assert.Equal(1, planCalls);
    }

    [Fact]
    public void ApplyComplete_Success_UninstallMode_TransitionsToFinishedUninstalled()
    {
        var vm = CreateViewModel(operationMode: InstallerOperationMode.Uninstall);

        vm.Begin();
        vm.OnApplyComplete(0);

        Assert.Equal(InstallerState.FinishedUninstalled, vm.State);
        Assert.Equal(InstallerScreen.Finished, vm.Screen);
        Assert.Equal("fluxo", vm.FinishedTitle);
        Assert.Equal("Thank you for letting fluxo help", vm.FinishedSubtitle);
        Assert.Equal("Uninstallation complete.", vm.StatusMessage);
    }

    [Fact]
    public void ApplyComplete_Failure_MaintenanceUninstall_ShowsUninstallFailureCopy()
    {
        var vm = CreateViewModel(operationMode: InstallerOperationMode.Maintenance);

        vm.Begin();
        vm.SelectedMaintenanceAction = InstallerMaintenanceAction.Uninstall;
        vm.ContinueMaintenanceCommand.Execute(null);
        vm.OnApplyComplete(1);

        Assert.Equal(InstallerState.FinishedFailed, vm.State);
        Assert.Equal("Uninstallation failed", vm.FinishedTitle);
        Assert.StartsWith("Uninstallation failed.", vm.StatusMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyComplete_Success_CopiesRepairerExecutable()
    {
        string? copiedSource = null;
        string? copiedDestination = null;
        bool? copiedOverwrite = null;

        var vm = new InstallerViewModel(
            dotNetRuntimeDetector: new FixedRuntimeDetector(true),
            fileExists: static _ => true,
            bundleExecutablePath: @"C:\Temp\fluxo-installer.exe",
            copyFile: (source, destination, overwrite) =>
            {
                copiedSource = source;
                copiedDestination = destination;
                copiedOverwrite = overwrite;
            });

        vm.OnApplyComplete(0);

        Assert.Equal(@"C:\Temp\fluxo-installer.exe", copiedSource);
        Assert.Equal(@"C:\Program Files\fluxo\fluxo.Repairer.exe", copiedDestination);
        Assert.True(copiedOverwrite);
        Assert.Equal(InstallerState.FinishedSuccess, vm.State);
    }

    [Fact]
    public void ApplyComplete_Success_DoesNotCopyRepairerOverItself()
    {
        var copyCalls = 0;
        var vm = new InstallerViewModel(
            dotNetRuntimeDetector: new FixedRuntimeDetector(true),
            fileExists: static _ => true,
            bundleExecutablePath: @"C:\Program Files\fluxo\fluxo.Repairer.exe",
            copyFile: (_, _, _) =>
            {
                copyCalls++;
                throw new InvalidOperationException("Copy should not be called.");
            });

        vm.OnApplyComplete(0);

        Assert.Equal(0, copyCalls);
        Assert.Equal(InstallerState.FinishedSuccess, vm.State);
    }

    [Fact]
    public void ApplyComplete_Success_TransitionsToFailed_When_ExeMissing()
    {
        var vm = CreateViewModel(fileExists: static _ => false);

        vm.OnApplyComplete(0);

        Assert.Equal(InstallerState.FinishedFailed, vm.State);
        Assert.Equal("Verification failed: Fluxo.exe was not found.", vm.StatusMessage);
        Assert.Equal(1, vm.ExitCode);
    }

    [Fact]
    public void ExitCode_DefaultsToCancel_When_NotInTerminalState()
    {
        var vm = CreateViewModel(fileExists: static _ => true);

        Assert.Equal(1602, vm.ExitCode);
    }

    private static InstallerViewModel CreateViewModel(
        Action<string>? setInstallFolderVariable = null,
        Action? requestDetect = null,
        Action? requestPlan = null,
        Action? requestApply = null,
        Func<string, bool>? fileExists = null,
        Func<bool>? requestRollback = null,
        Func<bool>? requestCancelConfirmation = null,
        InstallerOperationMode operationMode = InstallerOperationMode.Install,
        Action? closeInstallerAction = null)
    {
        return new InstallerViewModel(
            dotNetRuntimeDetector: new FixedRuntimeDetector(true),
            setInstallFolderVariable: setInstallFolderVariable,
            requestDetect: requestDetect,
            requestPlan: requestPlan,
            requestApply: requestApply,
            fileExists: fileExists,
            requestRollback: requestRollback,
            requestCancelConfirmation: requestCancelConfirmation,
            operationMode: operationMode,
            bundleExecutablePath: @"C:\Temp\fluxo-installer.exe",
            copyFile: static (_, _, _) => { },
            closeInstallerAction: closeInstallerAction);
    }

    private sealed class FixedRuntimeDetector(bool isInstalled) : IDotNetRuntimeDetector
    {
        public bool IsRequiredRuntimeInstalled() => isInstalled;
    }
}
