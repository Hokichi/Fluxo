# MainWindow Analytics Open Gating And State Fade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every Analytics open from MainWindow wait behind a single MainWindow-owned toast until control creation, data refresh, and first render settle complete; remove Analytics from hamburger; and fade Analytics visuals with MainWindow state transitions.

**Architecture:** Keep analytics data and rendering logic in `AnalyticsVM`/`Analytics`, but move open-flow feedback ownership to `MainWindow` for the MainWindow-triggered path. Add an explicit refresh entrypoint that can run with or without VM-owned toast, then orchestrate open in `MainWindow` with re-entrancy guards. Extend existing state-transition fade helpers to include analytics overlay visuals.

**Tech Stack:** C# 13, WPF (.NET 10), xUnit, NSubstitute

---

## File Structure And Responsibilities

- `C:/Users/Admins/source/repos/Fluxo/Fluxo/ViewModels/Shell/Main/AnalyticsVM.cs`
Responsibility: Add explicit open-refresh API with controllable toast ownership and preserve existing refresh behavior for internal VM-triggered refreshes.

- `C:/Users/Admins/source/repos/Fluxo/Fluxo/Views/Shell/Main/Analytics.xaml.cs`
Responsibility: Expose an awaitable open-preparation API, serialize concurrent preparations, and stop relying on implicit Loaded-triggered fetch for MainWindow open flow.

- `C:/Users/Admins/source/repos/Fluxo/Fluxo/Views/Shell/Main/MainWindow.xaml.cs`
Responsibility: Own async analytics open orchestration, show toast during every open, prevent duplicate opens, and include analytics visuals in state-change fade transitions.

- `C:/Users/Admins/source/repos/Fluxo/Fluxo/Views/Shell/Main/MainWindow.xaml`
Responsibility: Remove analytics action from header hamburger while keeping analytics drawer tab trigger.

- `C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/ViewModels/Popups/AnalyticsVMTests.cs`
Responsibility: Verify VM refresh behavior for `showToast: false` and `showToast: true` paths.

- `C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/Views/Shell/Main/MainWindowLayoutTests.cs`
Responsibility: Verify hamburger analytics action is removed and drawer-tab trigger remains in XAML.

### Task 1: Add failing tests for VM toast ownership and open refresh path

**Files:**
- Modify: `C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/ViewModels/Popups/AnalyticsVMTests.cs`

- [ ] **Step 1: Add `showToast: false` behavior test (no dialog toast, still refresh + settle)**

```csharp
[Fact]
public async Task RefreshForOpenAsync_ShowToastFalse_RefreshesAndSettlesWithoutDialogToast()
{
    var service = Substitute.For<IAnalyticsService>();
    service.GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
        .Returns(Task.FromResult(new AnalyticsDto(0m, 0m, [], [], [], [])));

    var dialogService = Substitute.For<IDialogService>();
    var uiSettleAwaiter = Substitute.For<IUiSettleAwaiter>();
    var vm = new AnalyticsVM(service, dialogService, uiSettleAwaiter);

    await vm.RefreshForOpenAsync(showToast: false, CancellationToken.None);

    await service.Received(1).GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    await uiSettleAwaiter.Received(1).WaitForUiReadyAsync(Arg.Any<Window?>(), Arg.Any<CancellationToken>());
    await dialogService.DidNotReceiveWithAnyArgs().ShowToastWhileAsync(default!, default(Func<Task>)!, default);
}
```

- [ ] **Step 2: Add `showToast: true` behavior test (VM toast wrapper still works)**

```csharp
[Fact]
public async Task RefreshForOpenAsync_ShowToastTrue_UsesDialogToastWrapper()
{
    var service = Substitute.For<IAnalyticsService>();
    service.GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
        .Returns(Task.FromResult(new AnalyticsDto(0m, 0m, [], [], [], [])));

    var dialogService = Substitute.For<IDialogService>();
    dialogService
        .ShowToastWhileAsync(Arg.Any<string>(), Arg.Any<Func<Task>>(), Arg.Any<Window?>())
        .Returns(callInfo => callInfo.ArgAt<Func<Task>>(1).Invoke());

    var uiSettleAwaiter = Substitute.For<IUiSettleAwaiter>();
    var vm = new AnalyticsVM(service, dialogService, uiSettleAwaiter);

    await vm.RefreshForOpenAsync(showToast: true, CancellationToken.None);

    await dialogService.Received(1)
        .ShowToastWhileAsync(Arg.Is<string>(message => message.StartsWith("Loading analytics")), Arg.Any<Func<Task>>(), Arg.Any<Window?>());
    await service.Received(1).GetAnalyticsAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
}
```

- [ ] **Step 3: Run targeted tests to confirm they fail before implementation**

Run: `dotnet test C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~AnalyticsVMTests"`
Expected: FAIL with compile/test errors about missing `RefreshForOpenAsync` or mismatched behavior.

- [ ] **Step 4: Commit failing-test checkpoint**

```bash
git add Fluxo.Tests/ViewModels/Popups/AnalyticsVMTests.cs
git commit -m "test: add analytics vm open-refresh toast ownership coverage"
```

### Task 2: Add failing XAML guard test for hamburger analytics removal

**Files:**
- Create: `C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/Views/Shell/Main/MainWindowLayoutTests.cs`

- [ ] **Step 1: Add static layout assertions for header menu and drawer tab**

```csharp
using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowLayoutTests
{
    private static readonly string MainWindowXamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Fluxo",
        "Views",
        "Shell",
        "Main",
        "MainWindow.xaml"));

    [Fact]
    public void HeaderMenu_DoesNotExposeAnalyticsActionButton()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);

        Assert.DoesNotContain("Click=\"OnAnalyticsButtonClick\"", xaml);
    }

    [Fact]
    public void AnalyticsDrawerTabTrigger_RemainsAvailable()
    {
        var xaml = File.ReadAllText(MainWindowXamlPath);

        Assert.Contains("Click=\"OnAnalyticsDrawerTabClick\"", xaml);
    }
}
```

- [ ] **Step 2: Run targeted test to verify initial failure**

Run: `dotnet test C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~MainWindowLayoutTests"`
Expected: FAIL because `MainWindow.xaml` still contains `Click="OnAnalyticsButtonClick"`.

- [ ] **Step 3: Commit failing-test checkpoint**

```bash
git add Fluxo.Tests/Views/Shell/Main/MainWindowLayoutTests.cs
git commit -m "test: add mainwindow layout guard for analytics menu action"
```

### Task 3: Implement AnalyticsVM and Analytics control open-preparation APIs

**Files:**
- Modify: `C:/Users/Admins/source/repos/Fluxo/Fluxo/ViewModels/Shell/Main/AnalyticsVM.cs`
- Modify: `C:/Users/Admins/source/repos/Fluxo/Fluxo/Views/Shell/Main/Analytics.xaml.cs`

- [ ] **Step 1: Add explicit VM refresh API for open flow and route `LoadAsync` through shared path**

```csharp
public async Task LoadAsync()
{
    await RefreshWithFeedbackAsync(CancellationToken.None, showToast: true);
}

public Task RefreshForOpenAsync(bool showToast, CancellationToken cancellationToken = default)
{
    return RefreshWithFeedbackAsync(cancellationToken, showToast);
}
```

- [ ] **Step 2: Update VM refresh feedback internals to support `showToast: false` without skipping settle**

```csharp
private async Task RefreshWithFeedbackAsync(CancellationToken cancellationToken, bool showToast)
{
    if (!showToast)
    {
        await RefreshAsync(cancellationToken);
        if (_uiSettleAwaiter is not null)
            await _uiSettleAwaiter.WaitForUiReadyAsync(cancellationToken: cancellationToken);

        return;
    }

    if (_dialogService is null || _uiSettleAwaiter is null)
    {
        await RefreshAsync(cancellationToken);
        return;
    }

    await _refreshFeedbackGate.WaitAsync(cancellationToken);
    try
    {
        await _dialogService.ShowToastWhileAsync(
            BuildAnalyticsLoadingMessage(),
            async () =>
            {
                await RefreshAsync(cancellationToken);
                await _uiSettleAwaiter.WaitForUiReadyAsync(cancellationToken: cancellationToken);
            });
    }
    finally
    {
        _refreshFeedbackGate.Release();
    }
}
```

- [ ] **Step 3: Add `Analytics.PrepareForOpenAsync` gate and stop automatic Loaded-based refresh**

```csharp
public partial class Analytics : UserControl
{
    private readonly AnalyticsVM _viewModel;
    private readonly SemaphoreSlim _openPreparationGate = new(1, 1);

    public Analytics(AnalyticsVM viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Unloaded += OnUnloaded;
    }

    public void ApplyOpenRange(DateTime from, DateTime to)
    {
        _viewModel.ApplyExternalDateRange(from, to, refresh: false);
    }

    public async Task PrepareForOpenAsync(bool showInternalToast, CancellationToken cancellationToken = default)
    {
        await _openPreparationGate.WaitAsync(cancellationToken);
        try
        {
            await _viewModel.RefreshForOpenAsync(showInternalToast, cancellationToken);
        }
        finally
        {
            _openPreparationGate.Release();
        }
    }
}
```

- [ ] **Step 4: Run VM-focused tests to verify implementation passes**

Run: `dotnet test C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~AnalyticsVMTests"`
Expected: PASS.

- [ ] **Step 5: Commit VM/control implementation**

```bash
git add Fluxo/ViewModels/Shell/Main/AnalyticsVM.cs Fluxo/Views/Shell/Main/Analytics.xaml.cs Fluxo.Tests/ViewModels/Popups/AnalyticsVMTests.cs
git commit -m "feat: add analytics open-refresh api with toast ownership control"
```

### Task 4: Implement MainWindow async open orchestration, hamburger cleanup, and state fade alignment

**Files:**
- Modify: `C:/Users/Admins/source/repos/Fluxo/Fluxo/Views/Shell/Main/MainWindow.xaml.cs`
- Modify: `C:/Users/Admins/source/repos/Fluxo/Fluxo/Views/Shell/Main/MainWindow.xaml`
- Modify: `C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/Views/Shell/Main/MainWindowLayoutTests.cs`

- [ ] **Step 1: Remove Analytics button block from header hamburger menu XAML**

```xml
<!-- Remove this entire block -->
<Button Click="OnAnalyticsButtonClick" Style="{StaticResource HeaderMenuActionButtonStyle}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="16" />
            <ColumnDefinition Width="12" />
            <ColumnDefinition Width="*" />
            <ColumnDefinition Width="8" />
            <ColumnDefinition Width="Auto" />
        </Grid.ColumnDefinitions>
        <components:Icon Width="14" Height="14" Path="{StaticResource Bank}" Color="{StaticResource Brush.Info}" />
        <TextBlock Grid.Column="2" VerticalAlignment="Center" FontFamily="{StaticResource Bold}" Foreground="{StaticResource Brush.Text.Primary}" Text="Analytics" />
        <TextBlock Grid.Column="4" Style="{StaticResource HeaderMenuShortcutTextStyle}" Text="Ctrl+A" />
    </Grid>
</Button>
```

- [ ] **Step 2: Replace sync open calls with guarded async orchestration in MainWindow**

```csharp
private bool _isPreparingAnalyticsOpen;

public void OpenAnalyticsPopup()
{
    _ = OpenAnalyticsPopupAsync();
}

private async Task OpenAnalyticsPopupAsync()
{
    if (_isAnalyticsDrawerOpen || _isAnalyticsDrawerTransitionActive || _isPreparingAnalyticsOpen)
        return;

    _isPreparingAnalyticsOpen = true;
    AnalyticsDrawerTabButton.IsEnabled = false;

    try
    {
        EnsureAnalyticsDrawerLoaded();
        ApplyMainWindowRangeToAnalyticsIfBounded();

        if (_analyticsDrawerView is null)
            return;

        await _dialogService.ShowToastWhileAsync(
            "Loading analytics",
            async () => await _analyticsDrawerView.PrepareForOpenAsync(showInternalToast: false),
            this);

        OpenAnalyticsDrawer();
    }
    catch (Exception exception)
    {
        _dialogService.ShowError($"Unable to open analytics.\n\n{exception.Message}", "Analytics", this);
    }
    finally
    {
        _isPreparingAnalyticsOpen = false;

        if (!_isAnalyticsDrawerOpen && !_isAnalyticsDrawerTransitionActive)
            AnalyticsDrawerTabButton.IsEnabled = true;
    }
}
```

- [ ] **Step 3: Update event handlers to use new async open path and remove obsolete handler**

```csharp
private async void OnAnalyticsDrawerTabClick(object sender, RoutedEventArgs e)
{
    CloseHeaderMenu();

    if (_isAnalyticsDrawerOpen)
    {
        CloseAnalyticsDrawer();
        return;
    }

    await OpenAnalyticsPopupAsync();
}

// Remove OnAnalyticsButtonClick entirely.

if (MainWindowShortcutMatcher.IsOpenAnalyticsShortcut(e.Key, Keyboard.Modifiers))
{
    _ = OpenAnalyticsPopupAsync();
    e.Handled = true;
    return;
}
```

- [ ] **Step 4: Fade analytics visuals with MainWindow state transitions**

```csharp
private void FadeContentOut(Action onCompleted)
{
    FadeElements(new UIElement[] { ContentGrid, AnalyticsDrawerLayer, AnalyticsDrawerTabHost }, 0, EasingMode.EaseIn, onCompleted);
}

private void FadeContentIn(Action? onCompleted = null)
{
    FadeElements(new UIElement[] { ContentGrid, AnalyticsDrawerLayer, AnalyticsDrawerTabHost }, 1, EasingMode.EaseOut, onCompleted);
}

private static void FadeElements(IReadOnlyList<UIElement> elements, double toOpacity, EasingMode easingMode, Action? onCompleted = null)
{
    if (elements.Count == 0)
    {
        onCompleted?.Invoke();
        return;
    }

    var pending = elements.Count;
    foreach (var element in elements)
    {
        FadeElement(element, toOpacity, easingMode, () =>
        {
            pending--;
            if (pending == 0)
                onCompleted?.Invoke();
        });
    }
}
```

- [ ] **Step 5: Run layout tests to verify hamburger analytics removal and drawer trigger retention**

Run: `dotnet test C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~MainWindowLayoutTests"`
Expected: PASS.

- [ ] **Step 6: Commit MainWindow/XAML changes**

```bash
git add Fluxo/Views/Shell/Main/MainWindow.xaml Fluxo/Views/Shell/Main/MainWindow.xaml.cs Fluxo.Tests/Views/Shell/Main/MainWindowLayoutTests.cs
git commit -m "feat: gate analytics open with mainwindow toast and align state fade"
```

### Task 5: Run focused regression tests and manual verification checklist

**Files:**
- Modify: `C:/Users/Admins/source/repos/Fluxo/docs/superpowers/plans/2026-04-25-mainwindow-analytics-open-gating-and-state-fade.md`

- [x] **Step 1: Run focused automated regression suite**

Run: `dotnet test C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~AnalyticsVMTests|FullyQualifiedName~MainWindowLayoutTests|FullyQualifiedName~MainWindowShortcutMatcherTests|FullyQualifiedName~MainWindowStateFadeGuardTests"`
Expected: PASS for all selected tests.

- [ ] **Step 2: Execute manual UI checks on the built app**

Manual checks:
1. Click analytics drawer tab when closed: MainWindow toast appears, then drawer opens after load completes.
2. Close drawer and reopen from tab: toast appears again and no nested second toast appears.
3. Press `Ctrl+A` when drawer is closed: identical gated open behavior.
4. Open header hamburger: analytics action is absent.
5. With analytics drawer visible, trigger maximize and restore: analytics layer and tab fade in sync with the main content transition.

- [x] **Step 3: Record verification summary in this plan file**

- [x] **Step 4: Commit verification notes**

```bash
git add -f docs/superpowers/plans/2026-04-25-mainwindow-analytics-open-gating-and-state-fade.md
git commit -m "docs: record analytics open-gating and fade verification"
```

## Verification Results

- Date: 2026-04-25
- Automated test suite status: PASS
  - Command: `dotnet test C:/Users/Admins/source/repos/Fluxo/Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~AnalyticsVMTests|FullyQualifiedName~MainWindowLayoutTests|FullyQualifiedName~MainWindowShortcutMatcherTests|FullyQualifiedName~MainWindowStateFadeGuardTests"`
  - Result: `Passed!  - Failed: 0, Passed: 22, Skipped: 0, Total: 22`
- Analytics open after toast-wrapped prepare: Pending manual UI verification (not executed in this CLI session).
- Reopen single-toast behavior: Pending manual UI verification (not executed in this CLI session).
- Ctrl+A path: Pending manual UI verification (not executed in this CLI session).
- Hamburger analytics removal: Covered by `MainWindowLayoutTests` in the passing focused suite; manual UI confirmation still pending in this CLI session.
- Analytics fade with window state transition: Pending manual UI verification (not executed in this CLI session).

## Plan Self-Review

### Spec coverage
- Every MainWindow open path (tab and Ctrl+A) is explicitly routed through toast-gated async open orchestration.
- Duplicate-toast prevention is covered by `showInternalToast: false` in MainWindow open path and dedicated VM tests.
- Hamburger Analytics removal is covered by XAML change and layout test.
- State-change fade alignment for analytics visuals is covered by shared fade-target helper update and manual verification.

### Placeholder scan
- No `TODO`, `TBD`, ellipses, or vague placeholders.
- Each code-change step contains concrete code snippets.
- Every test/run step includes exact command and expected result.

### Type consistency
- Method names are consistent across tasks: `RefreshForOpenAsync`, `PrepareForOpenAsync`, and `OpenAnalyticsPopupAsync`.
- Toast ownership flags are consistently named as `showToast`/`showInternalToast`.
- Fade helper names and call sites stay consistent with existing `FadeElement` usage.
