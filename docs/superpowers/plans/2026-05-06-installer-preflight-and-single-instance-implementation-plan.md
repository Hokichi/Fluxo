# Installer Preflight and Single-Instance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce installer preflight termination for install/repair/uninstall, restore reliable up-to-date install short-circuiting, and guarantee a single active Fluxo app instance that foregrounds the existing window on relaunch.

**Architecture:** Consolidate installer process-gating into one preflight method in `InstallerViewModel`, isolate version-skip decision into a pure helper that `BootstrapperEntry` consumes, and add a startup single-instance coordinator (mutex + named-pipe activation signal) wired into `App.xaml.cs`.

**Tech Stack:** C# 14, WPF (.NET 10), WiX Burn bootstrapper API, xUnit.

---

## Scope check

The approved spec spans two related surfaces (`Fluxo.Installer` and `Fluxo` app startup). They are independent at runtime but coupled in release behavior, so this plan keeps them in one execution track with isolated tasks and separate commits.

## File structure map

- `Fluxo.Installer/ViewModels/InstallerViewModel.cs`
- Responsibility: central preflight gate for running Fluxo processes before install/repair/uninstall and operation-specific failure messaging.

- `Fluxo.Installer/Models/InstallerUpToDateDecision.cs` (new)
- Responsibility: pure decision helper for install short-circuit conditions (`same-or-newer` check only in install mode).

- `Fluxo.Installer/BootstrapperEntry.cs`
- Responsibility: gather version signals from Burn detect events and delegate skip decision to helper.

- `Fluxo.Tests/Installer/InstallerFlowStateTests.cs`
- Responsibility: installer preflight behavior tests for install/repair/uninstall branches.

- `Fluxo.Tests/Installer/InstallerUpToDateDecisionTests.cs` (new)
- Responsibility: decision-table tests for version short-circuiting.

- `Fluxo/Infrastructure/SingleInstance/ISingleInstanceCoordinator.cs` (new)
- Responsibility: abstraction for app startup to acquire primary role and trigger activation of existing instance.

- `Fluxo/Infrastructure/SingleInstance/SingleInstanceCoordinator.cs` (new)
- Responsibility: mutex + named-pipe implementation and activation listener.

- `Fluxo/App.xaml.cs`
- Responsibility: wire single-instance startup gate and activation callback (`RestoreMainWindowFromTray` + foreground).

- `Fluxo.Tests/Infrastructure/SingleInstance/SingleInstanceStartupPolicyTests.cs` (new)
- Responsibility: startup behavior tests via coordinator abstraction without OS-level mutex/pipe dependencies.

### Task 1: Add failing installer preflight tests for install and repair

**Files:**
- Modify: `Fluxo.Tests/Installer/InstallerFlowStateTests.cs`
- Test: `Fluxo.Tests/Installer/InstallerFlowStateTests.cs`

- [ ] **Step 1: Write the failing tests (install/repair blocked when running and user declines)**

```csharp
[Fact]
public void Install_WhenFluxoRunningAndUserDeclinesTermination_BlocksBeforeDetect()
{
    var detectCalls = 0;
    var vm = CreateViewModel(
        requestDetect: () => detectCalls++,
        getRunningFluxoProcessIds: static () => [1234],
        requestTerminateRunningAppConfirmation: () => false,
        fileExists: static _ => true);

    vm.InstallCommand.Execute(null);

    Assert.Equal(InstallerState.FinishedFailed, vm.State);
    Assert.Equal(InstallerScreen.Finished, vm.Screen);
    Assert.Equal(0, detectCalls);
    Assert.Equal(
        "Installation did not run because fluxo is still open. Please close fluxo and run setup again.",
        vm.StatusMessage);
}

[Fact]
public void ContinueMaintenance_Repair_WhenFluxoRunningAndUserDeclinesTermination_BlocksBeforeDetect()
{
    var detectCalls = 0;
    var vm = CreateViewModel(
        requestDetect: () => detectCalls++,
        operationMode: InstallerOperationMode.Maintenance,
        getRunningFluxoProcessIds: static () => [1234],
        requestTerminateRunningAppConfirmation: () => false,
        fileExists: static _ => true);

    vm.Begin();
    vm.SelectedMaintenanceAction = InstallerMaintenanceAction.Repair;
    vm.ContinueMaintenanceCommand.Execute(null);

    Assert.Equal(InstallerState.FinishedFailed, vm.State);
    Assert.Equal(InstallerScreen.Finished, vm.Screen);
    Assert.Equal(InstallerRequestedOperation.Repair, vm.RequestedOperation);
    Assert.Equal(0, detectCalls);
    Assert.Equal(
        "Repair did not run because fluxo is still open. Please close fluxo and run the repairer again.",
        vm.StatusMessage);
}
```

- [ ] **Step 2: Run targeted tests to verify they fail**

Run: `dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj --filter "Install_WhenFluxoRunningAndUserDeclinesTermination_BlocksBeforeDetect|ContinueMaintenance_Repair_WhenFluxoRunningAndUserDeclinesTermination_BlocksBeforeDetect"`
Expected: FAIL because current code only enforces termination gate for uninstall.

- [ ] **Step 3: Commit test-only change**

```bash
git add Fluxo.Tests/Installer/InstallerFlowStateTests.cs
git commit -m "test(installer): add failing preflight tests for install and repair"
```

### Task 2: Implement shared installer preflight gate for all operations

**Files:**
- Modify: `Fluxo.Installer/ViewModels/InstallerViewModel.cs`
- Modify: `Fluxo.Tests/Installer/InstallerFlowStateTests.cs`
- Test: `Fluxo.Tests/Installer/InstallerFlowStateTests.cs`

- [ ] **Step 1: Implement shared preflight method and operation-specific failure copy**

```csharp
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

    if (!requestTerminateRunningAppConfirmation())
    {
        BlockOperationAndFinish(
            operation,
            "is still open",
            retrySuffix: operation == InstallerRequestedOperation.Install
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
                retrySuffix: operation == InstallerRequestedOperation.Install
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
```

- [ ] **Step 2: Route install/repair/uninstall entry points through shared preflight**

```csharp
private void Install()
{
    if (IsMaintenanceMode || IsUninstallMode)
    {
        return;
    }

    if (!EnsureFluxoCanBeStoppedForOperation(InstallerRequestedOperation.Install))
    {
        return;
    }

    RequestedOperation = InstallerRequestedOperation.Install;
    // existing flow continues
}

private void StartUninstall()
{
    RequestedOperation = InstallerRequestedOperation.Uninstall;
    installingChecklistStep.Label = InstallingChecklistLabel;

    if (!EnsureFluxoCanBeStoppedForOperation(InstallerRequestedOperation.Uninstall))
    {
        return;
    }

    // existing uninstall flow continues
}

private void ContinueMaintenance()
{
    RequestedOperation = SelectedMaintenanceAction == InstallerMaintenanceAction.Uninstall
        ? InstallerRequestedOperation.Uninstall
        : InstallerRequestedOperation.Repair;

    if (!EnsureFluxoCanBeStoppedForOperation(RequestedOperation))
    {
        return;
    }

    // existing detect transition continues
}
```

- [ ] **Step 3: Add/adjust termination-failure tests for install and repair**

```csharp
[Fact]
public void Install_WhenTerminationFails_BlocksBeforeDetect()
{
    var detectCalls = 0;
    var vm = CreateViewModel(
        requestDetect: () => detectCalls++,
        getRunningFluxoProcessIds: static () => [1234],
        requestTerminateRunningAppConfirmation: () => true,
        tryTerminateProcessById: _ => false,
        fileExists: static _ => true);

    vm.InstallCommand.Execute(null);

    Assert.Equal(InstallerState.FinishedFailed, vm.State);
    Assert.Equal(0, detectCalls);
    Assert.Equal(
        "Installation did not run because fluxo could not be terminated. Please close fluxo and run setup again.",
        vm.StatusMessage);
}
```

- [ ] **Step 4: Run installer test suite**

Run: `dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~Fluxo.Tests.Installer"`
Expected: PASS for installer tests, including new preflight coverage.

- [ ] **Step 5: Commit implementation and tests**

```bash
git add Fluxo.Installer/ViewModels/InstallerViewModel.cs Fluxo.Tests/Installer/InstallerFlowStateTests.cs
git commit -m "feat(installer): enforce running-process preflight for install repair and uninstall"
```

### Task 3: Add failing version short-circuit decision tests

**Files:**
- Create: `Fluxo.Tests/Installer/InstallerUpToDateDecisionTests.cs`
- Test: `Fluxo.Tests/Installer/InstallerUpToDateDecisionTests.cs`

- [ ] **Step 1: Write decision-table tests for skip logic**

```csharp
using Fluxo.Installer.Models;
using Xunit;

namespace Fluxo.Tests.Installer;

public sealed class InstallerUpToDateDecisionTests
{
    [Fact]
    public void Install_SameVersion_ShouldSkip()
    {
        var skip = InstallerUpToDateDecision.ShouldSkipInstall(
            InstallerOperationMode.Install,
            detectStatus: 0,
            currentBundleVersion: "1.0.0.0",
            highestDetectedInstalledVersion: "1.0.0.0",
            compareVersions: static (left, right) => string.CompareOrdinal(left, right));

        Assert.True(skip);
    }

    [Fact]
    public void Install_HigherInstalledVersion_ShouldSkip()
    {
        var skip = InstallerUpToDateDecision.ShouldSkipInstall(
            InstallerOperationMode.Install,
            detectStatus: 0,
            currentBundleVersion: "1.0.0.0",
            highestDetectedInstalledVersion: "1.1.0.0",
            compareVersions: static (_, _) => 1);

        Assert.True(skip);
    }

    [Fact]
    public void Repair_WithSameVersion_ShouldNotSkip()
    {
        var skip = InstallerUpToDateDecision.ShouldSkipInstall(
            InstallerOperationMode.Maintenance,
            detectStatus: 0,
            currentBundleVersion: "1.0.0.0",
            highestDetectedInstalledVersion: "1.0.0.0",
            compareVersions: static (_, _) => 0);

        Assert.False(skip);
    }

    [Fact]
    public void Uninstall_WithHigherVersion_ShouldNotSkip()
    {
        var skip = InstallerUpToDateDecision.ShouldSkipInstall(
            InstallerOperationMode.Uninstall,
            detectStatus: 0,
            currentBundleVersion: "1.0.0.0",
            highestDetectedInstalledVersion: "2.0.0.0",
            compareVersions: static (_, _) => 1);

        Assert.False(skip);
    }
}
```

- [ ] **Step 2: Run tests to verify compile/runtime failure**

Run: `dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~InstallerUpToDateDecisionTests"`
Expected: FAIL because `InstallerUpToDateDecision` does not yet exist.

- [ ] **Step 3: Commit failing tests**

```bash
git add Fluxo.Tests/Installer/InstallerUpToDateDecisionTests.cs
git commit -m "test(installer): add failing tests for up-to-date skip decision"
```

### Task 4: Implement version decision helper and wire bootstrapper usage

**Files:**
- Create: `Fluxo.Installer/Models/InstallerUpToDateDecision.cs`
- Modify: `Fluxo.Installer/BootstrapperEntry.cs`
- Test: `Fluxo.Tests/Installer/InstallerUpToDateDecisionTests.cs`

- [ ] **Step 1: Implement pure decision helper**

```csharp
namespace Fluxo.Installer.Models;

public static class InstallerUpToDateDecision
{
    public static bool ShouldSkipInstall(
        InstallerOperationMode operationMode,
        int detectStatus,
        string? currentBundleVersion,
        string? highestDetectedInstalledVersion,
        Func<string, string, int> compareVersions)
    {
        if (operationMode != InstallerOperationMode.Install || detectStatus != 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentBundleVersion)
            || string.IsNullOrWhiteSpace(highestDetectedInstalledVersion))
        {
            return false;
        }

        return compareVersions(highestDetectedInstalledVersion, currentBundleVersion) >= 0;
    }
}
```

- [ ] **Step 2: Consume helper in `OnDetectComplete`**

```csharp
private void OnDetectComplete(object? sender, DetectCompleteEventArgs e)
{
    var operationMode = GetOperationMode();
    var shouldSkipInstall = InstallerUpToDateDecision.ShouldSkipInstall(
        operationMode,
        e.Status,
        _currentBundleVersion,
        _highestDetectedInstalledVersion,
        (left, right) => engine.CompareVersions(left, right));

    if (_headlessMode)
    {
        if (shouldSkipInstall)
        {
            _headlessExitCode = SuccessExitCode;
            _headlessCompleted.Set();
            return;
        }

        engine.Plan(GetRequestedLaunchAction(), GetRequestedBundleScope());
        return;
    }

    if (shouldSkipInstall)
    {
        DispatchToUi(() => _viewModel?.OnDetectedUpToDateVersion());
        return;
    }

    DispatchToUi(() => _viewModel?.OnDetectComplete(e.Status));
}
```

- [ ] **Step 3: Run decision and installer test suites**

Run: `dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~Fluxo.Tests.Installer"`
Expected: PASS with install-only short-circuit preserved.

- [ ] **Step 4: Commit helper + wiring**

```bash
git add Fluxo.Installer/Models/InstallerUpToDateDecision.cs Fluxo.Installer/BootstrapperEntry.cs Fluxo.Tests/Installer/InstallerUpToDateDecisionTests.cs
git commit -m "fix(installer): isolate and harden install up-to-date skip decision"
```

### Task 5: Add failing app startup single-instance tests (abstraction-level)

**Files:**
- Create: `Fluxo.Tests/Infrastructure/SingleInstance/SingleInstanceStartupPolicyTests.cs`
- Test: `Fluxo.Tests/Infrastructure/SingleInstance/SingleInstanceStartupPolicyTests.cs`

- [ ] **Step 1: Write failing tests around startup decisions using interface stubs**

```csharp
using Xunit;

namespace Fluxo.Tests.Infrastructure.SingleInstance;

public sealed class SingleInstanceStartupPolicyTests
{
    [Fact]
    public void SecondaryInstance_ShouldRequestActivationAndAbortStartup()
    {
        var coordinator = new FakeSingleInstanceCoordinator(isPrimaryInstance: false);

        var shouldContinue = coordinator.TryEnterAsPrimary(onActivationRequested: () => { });

        Assert.False(shouldContinue);
        Assert.Equal(1, coordinator.SignalExistingInstanceCalls);
    }

    [Fact]
    public void PrimaryInstance_ShouldContinueStartup_AndInvokeActivationCallback()
    {
        var activationCalls = 0;
        var coordinator = new FakeSingleInstanceCoordinator(isPrimaryInstance: true);

        var shouldContinue = coordinator.TryEnterAsPrimary(onActivationRequested: () => activationCalls++);
        coordinator.RaiseActivation();

        Assert.True(shouldContinue);
        Assert.Equal(1, activationCalls);
    }

    private sealed class FakeSingleInstanceCoordinator
    {
        private readonly bool _isPrimary;
        private Action? _activation;

        public FakeSingleInstanceCoordinator(bool isPrimaryInstance)
        {
            _isPrimary = isPrimaryInstance;
        }

        public int SignalExistingInstanceCalls { get; private set; }

        public bool TryEnterAsPrimary(Action onActivationRequested)
        {
            _activation = onActivationRequested;
            if (_isPrimary)
            {
                return true;
            }

            SignalExistingInstanceCalls++;
            return false;
        }

        public void RaiseActivation() => _activation?.Invoke();
    }
}
```

- [ ] **Step 2: Run tests to verify red state (missing production abstraction wiring)**

Run: `dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~SingleInstanceStartupPolicyTests"`
Expected: FAIL after replacing fake with production abstraction references in next step.

- [ ] **Step 3: Commit test scaffold if split commit is preferred**

```bash
git add Fluxo.Tests/Infrastructure/SingleInstance/SingleInstanceStartupPolicyTests.cs
git commit -m "test(app): add failing single-instance startup policy tests"
```

### Task 6: Implement single-instance coordinator and wire `App` startup

**Files:**
- Create: `Fluxo/Infrastructure/SingleInstance/ISingleInstanceCoordinator.cs`
- Create: `Fluxo/Infrastructure/SingleInstance/SingleInstanceCoordinator.cs`
- Modify: `Fluxo/App.xaml.cs`
- Modify: `Fluxo.Tests/Infrastructure/SingleInstance/SingleInstanceStartupPolicyTests.cs`

- [ ] **Step 1: Add coordinator contract and implementation skeleton**

```csharp
namespace Fluxo.Infrastructure.SingleInstance;

public interface ISingleInstanceCoordinator : IDisposable
{
    bool TryEnterAsPrimary(Action onActivationRequested);
}
```

```csharp
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Fluxo.Infrastructure.SingleInstance;

public sealed class SingleInstanceCoordinator : ISingleInstanceCoordinator
{
    private readonly string _mutexName;
    private readonly string _pipeName;
    private Mutex? _mutex;
    private CancellationTokenSource? _listenerCts;

    public SingleInstanceCoordinator(string appKey = "Fluxo")
    {
        _mutexName = $@"Local\{appKey}.SingleInstance";
        _pipeName = $"{appKey}.Activate";
    }

    public bool TryEnterAsPrimary(Action onActivationRequested)
    {
        _mutex = new Mutex(initiallyOwned: true, _mutexName, out var createdNew);
        if (!createdNew)
        {
            TrySignalExistingInstance();
            return false;
        }

        _listenerCts = new CancellationTokenSource();
        _ = ListenAsync(onActivationRequested, _listenerCts.Token);
        return true;
    }

    private async Task ListenAsync(Action onActivationRequested, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            onActivationRequested();
        }
    }

    private void TrySignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(timeout: 250);
            client.WriteByte(1);
            client.Flush();
        }
        catch
        {
            // Best-effort activation signal.
        }
    }

    public void Dispose()
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        _mutex?.Dispose();
    }
}
```

- [ ] **Step 2: Wire startup gate in `App.xaml.cs` before heavy initialization**

```csharp
private ISingleInstanceCoordinator? _singleInstanceCoordinator;

protected override async void OnStartup(StartupEventArgs e)
{
    _singleInstanceCoordinator ??= new SingleInstanceCoordinator();
    var isPrimary = _singleInstanceCoordinator.TryEnterAsPrimary(() =>
    {
        Dispatcher.BeginInvoke(() =>
        {
            RestoreMainWindowFromTray();
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.ShowFromTray();
            }
        });
    });

    if (!isPrimary)
    {
        Shutdown();
        return;
    }

    base.OnStartup(e);
    // existing startup flow continues
}

protected override void OnExit(ExitEventArgs e)
{
    _singleInstanceCoordinator?.Dispose();
    _singleInstanceCoordinator = null;
    base.OnExit(e);
}
```

- [ ] **Step 3: Update tests to use `ISingleInstanceCoordinator` abstraction instead of local fake-only behavior**

```csharp
private sealed class TestCoordinator : ISingleInstanceCoordinator
{
    private readonly bool _isPrimary;
    private Action? _onActivate;

    public TestCoordinator(bool isPrimary)
    {
        _isPrimary = isPrimary;
    }

    public int EnterCalls { get; private set; }

    public bool TryEnterAsPrimary(Action onActivationRequested)
    {
        EnterCalls++;
        _onActivate = onActivationRequested;
        return _isPrimary;
    }

    public void TriggerActivation() => _onActivate?.Invoke();

    public void Dispose() { }
}
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit single-instance implementation**

```bash
git add Fluxo/Infrastructure/SingleInstance/ISingleInstanceCoordinator.cs Fluxo/Infrastructure/SingleInstance/SingleInstanceCoordinator.cs Fluxo/App.xaml.cs Fluxo.Tests/Infrastructure/SingleInstance/SingleInstanceStartupPolicyTests.cs
git commit -m "feat(app): enforce single-instance startup with activation signal"
```

### Task 7: Final verification and packaging checks

**Files:**
- Modify: none expected
- Test: installer + app tests and build

- [ ] **Step 1: Run focused installer tests**

Run: `dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj --filter "FullyQualifiedName~Fluxo.Tests.Installer"`
Expected: PASS.

- [ ] **Step 2: Run full test suite**

Run: `dotnet test .\Fluxo.Tests\Fluxo.Tests.csproj`
Expected: PASS.

- [ ] **Step 3: Build installer projects**

Run: `dotnet build .\Fluxo.Installer\Fluxo.Installer.csproj -c Release`
Expected: Build succeeded.

Run: `dotnet build .\Fluxo.Installer.Bundle\Fluxo.Installer.Bundle.wixproj -c Release`
Expected: Build succeeded.

- [ ] **Step 4: Commit verification artifacts only if files changed intentionally**

```bash
git status --short
```

Expected: no unintended source changes.

---

## Plan self-review

### Spec coverage

- Installer no-op on latest version: covered by Tasks 3-4.
- Termination prompt for install/repair/uninstall at operation start: covered by Tasks 1-2.
- Single active Fluxo instance with activate-existing behavior: covered by Tasks 5-6.
- Verification and regression safety: covered by Task 7.

### Placeholder scan

- No `TODO`, `TBD`, or “implement later” placeholders remain.
- Each code step includes concrete snippets, each run step includes explicit command + expected outcome.

### Type/signature consistency

- `InstallerRequestedOperation` used consistently in shared preflight flow.
- `ISingleInstanceCoordinator.TryEnterAsPrimary(Action)` signature is consistent in implementation and tests.
- App activation callback calls existing `RestoreMainWindowFromTray()` path.
