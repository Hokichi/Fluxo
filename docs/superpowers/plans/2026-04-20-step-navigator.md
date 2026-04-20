# Step Navigator Control Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a reusable `StepNavigatorControl` WPF custom control that renders an animated pill-dot progress indicator, and migrate the Startup Wizard to use it.

**Architecture:** A `Control` subclass exposes `StepCount` and `CurrentStep` dependency properties and manages an internal `ObservableCollection<StepNavigatorDotVM>` (read-only DP). When either property changes, `UpdateDotStates` recomputes `IsActive`/`IsCompleted` on each dot in-place. The XAML template renders a pill `Border` per dot with DataTrigger-driven width animation and color Setters, plus a connector `Border` between each pair of dots.

**Tech Stack:** .NET 10, WPF, C#, CommunityToolkit.Mvvm, xUnit

---

## File Map

| Action | Path | Responsibility |
|---|---|---|
| Create | `Fluxo/ViewModels/CustomControls/StepNavigatorDotVM.cs` | Observable VM with `IsActive`, `IsCompleted`, `IsFirst` |
| Create | `Fluxo/Views/CustomControls/StepNavigatorControl.cs` | Custom control: DPs, dot state logic |
| Create | `Fluxo/Resources/Styles/StepNavigatorStyle.xaml` | Control template, pill animation, connector |
| Create | `Fluxo.Tests/ViewModels/CustomControls/StepNavigatorControlTests.cs` | Unit tests for `UpdateDotStates` |
| Modify | `Fluxo/App.xaml` | Merge `StepNavigatorStyle.xaml` into global resources |
| Modify | `Fluxo/ViewModels/Shell/StartupWizard/StartupWizardVM.cs` | Add `CurrentStep` property, remove `StepDots` |
| Modify | `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml` | Replace `ItemsControl` navigator with `StepNavigatorControl` |
| Modify | `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml.cs` | Remove `OnDotClick` handler and its `using` alias |
| Modify | `Fluxo/Resources/Styles/StartupWizardStyle.xaml` | Remove `WizardStepDotStyle` |
| Delete | `Fluxo/ViewModels/Shell/StartupWizard/StartupWizardStepDotVM.cs` | Superseded |

---

## Task 1: StepNavigatorDotVM

**Files:**
- Create: `Fluxo/ViewModels/CustomControls/StepNavigatorDotVM.cs`

- [ ] **Step 1: Create the VM file**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.CustomControls;

public sealed partial class StepNavigatorDotVM : ObservableObject
{
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isCompleted;
    public bool IsFirst { get; init; }
}
```

- [ ] **Step 2: Commit**

```bash
git add Fluxo/ViewModels/CustomControls/StepNavigatorDotVM.cs
git commit -m "feat: add StepNavigatorDotVM"
```

---

## Task 2: StepNavigatorControl — failing tests first

**Files:**
- Create: `Fluxo.Tests/ViewModels/CustomControls/StepNavigatorControlTests.cs`

- [ ] **Step 1: Create the test file**

```csharp
using System.Collections.ObjectModel;
using Fluxo.ViewModels.CustomControls;
using Fluxo.Views.CustomControls;
using Xunit;

namespace Fluxo.Tests.ViewModels.CustomControls;

public sealed class StepNavigatorControlTests
{
    [Fact]
    public void UpdateDotStates_CurrentStep1_FirstDotActiveOthersUpcoming()
    {
        var dots = MakeDots(3);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 1);

        Assert.True(dots[0].IsActive);
        Assert.False(dots[0].IsCompleted);
        Assert.False(dots[1].IsActive);
        Assert.False(dots[1].IsCompleted);
        Assert.False(dots[2].IsActive);
        Assert.False(dots[2].IsCompleted);
    }

    [Fact]
    public void UpdateDotStates_CurrentStep2_FirstCompletedSecondActive()
    {
        var dots = MakeDots(3);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 2);

        Assert.False(dots[0].IsActive);
        Assert.True(dots[0].IsCompleted);
        Assert.True(dots[1].IsActive);
        Assert.False(dots[1].IsCompleted);
        Assert.False(dots[2].IsActive);
        Assert.False(dots[2].IsCompleted);
    }

    [Fact]
    public void UpdateDotStates_CurrentStep3_TwoCompletedLastActive()
    {
        var dots = MakeDots(3);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 3);

        Assert.False(dots[0].IsActive);
        Assert.True(dots[0].IsCompleted);
        Assert.False(dots[1].IsActive);
        Assert.True(dots[1].IsCompleted);
        Assert.True(dots[2].IsActive);
        Assert.False(dots[2].IsCompleted);
    }

    [Fact]
    public void UpdateDotStates_NavigateBackward_PreviousCompletedBecomesUpcoming()
    {
        var dots = MakeDots(3);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 3);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 2);

        Assert.False(dots[0].IsActive);
        Assert.True(dots[0].IsCompleted);
        Assert.True(dots[1].IsActive);
        Assert.False(dots[1].IsCompleted);
        Assert.False(dots[2].IsActive);
        Assert.False(dots[2].IsCompleted);
    }

    [Fact]
    public void UpdateDotStates_ExactlyOneDotIsActive()
    {
        var dots = MakeDots(5);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 3);

        Assert.Equal(1, dots.Count(d => d.IsActive));
    }

    [Fact]
    public void UpdateDotStates_ActiveDotIsNeverCompleted()
    {
        var dots = MakeDots(5);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 3);

        Assert.False(dots[2].IsActive && dots[2].IsCompleted);
    }

    private static ObservableCollection<StepNavigatorDotVM> MakeDots(int count)
    {
        var dots = new ObservableCollection<StepNavigatorDotVM>();
        for (var i = 0; i < count; i++)
            dots.Add(new StepNavigatorDotVM { IsFirst = i == 0 });
        return dots;
    }
}
```

- [ ] **Step 2: Run tests — expect compile failure**

```bash
dotnet test Fluxo.Tests --filter "StepNavigatorControlTests" 2>&1 | head -20
```

Expected: build error — `StepNavigatorControl` does not exist yet.

- [ ] **Step 3: Commit failing tests**

```bash
git add Fluxo.Tests/ViewModels/CustomControls/StepNavigatorControlTests.cs
git commit -m "test: add failing tests for StepNavigatorControl.UpdateDotStates"
```

---

## Task 3: StepNavigatorControl — implementation

**Files:**
- Create: `Fluxo/Views/CustomControls/StepNavigatorControl.cs`

- [ ] **Step 1: Create the control**

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Fluxo.ViewModels.CustomControls;

namespace Fluxo.Views.CustomControls;

public sealed class StepNavigatorControl : Control
{
    static StepNavigatorControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(StepNavigatorControl),
            new FrameworkPropertyMetadata(typeof(StepNavigatorControl)));
    }

    public static readonly DependencyProperty StepCountProperty =
        DependencyProperty.Register(
            nameof(StepCount),
            typeof(int),
            typeof(StepNavigatorControl),
            new PropertyMetadata(0, OnStepCountChanged));

    public static readonly DependencyProperty CurrentStepProperty =
        DependencyProperty.Register(
            nameof(CurrentStep),
            typeof(int),
            typeof(StepNavigatorControl),
            new PropertyMetadata(0, OnCurrentStepChanged));

    private static readonly DependencyPropertyKey DotsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Dots),
            typeof(ObservableCollection<StepNavigatorDotVM>),
            typeof(StepNavigatorControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DotsProperty = DotsPropertyKey.DependencyProperty;

    public StepNavigatorControl()
    {
        SetValue(DotsPropertyKey, new ObservableCollection<StepNavigatorDotVM>());
    }

    public int StepCount
    {
        get => (int)GetValue(StepCountProperty);
        set => SetValue(StepCountProperty, value);
    }

    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    public ObservableCollection<StepNavigatorDotVM> Dots =>
        (ObservableCollection<StepNavigatorDotVM>)GetValue(DotsProperty);

    private static void OnStepCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (StepNavigatorControl)d;
        var dots = control.Dots;
        dots.Clear();

        var count = (int)e.NewValue;
        for (var i = 0; i < count; i++)
            dots.Add(new StepNavigatorDotVM { IsFirst = i == 0 });

        UpdateDotStates(dots, control.CurrentStep);
    }

    private static void OnCurrentStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (StepNavigatorControl)d;
        UpdateDotStates(control.Dots, (int)e.NewValue);
    }

    internal static void UpdateDotStates(ObservableCollection<StepNavigatorDotVM> dots, int currentStep)
    {
        for (var i = 0; i < dots.Count; i++)
        {
            dots[i].IsCompleted = i < currentStep - 1;
            dots[i].IsActive = i == currentStep - 1;
        }
    }
}
```

- [ ] **Step 2: Run tests — expect pass**

```bash
dotnet test Fluxo.Tests --filter "StepNavigatorControlTests" 2>&1 | tail -10
```

Expected: all 6 tests pass.

- [ ] **Step 3: Commit**

```bash
git add Fluxo/Views/CustomControls/StepNavigatorControl.cs
git commit -m "feat: implement StepNavigatorControl with UpdateDotStates"
```

---

## Task 4: StepNavigatorStyle.xaml — control template

**Files:**
- Create: `Fluxo/Resources/Styles/StepNavigatorStyle.xaml`

- [ ] **Step 1: Create the template**

```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:customControls="clr-namespace:Fluxo.Views.CustomControls">

    <Style TargetType="{x:Type customControls:StepNavigatorControl}">
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="{x:Type customControls:StepNavigatorControl}">
                    <ItemsControl IsHitTestVisible="False" ItemsSource="{TemplateBinding Dots}">
                        <ItemsControl.ItemsPanel>
                            <ItemsPanelTemplate>
                                <StackPanel Orientation="Horizontal" VerticalAlignment="Center" />
                            </ItemsPanelTemplate>
                        </ItemsControl.ItemsPanel>
                        <ItemsControl.ItemTemplate>
                            <DataTemplate>
                                <StackPanel Orientation="Horizontal" VerticalAlignment="Center">

                                    <!--  Left connector — hidden for the first dot  -->
                                    <Border
                                        Width="8"
                                        Height="2"
                                        Visibility="{Binding IsFirst, Converter={StaticResource BoolToVisibilityInvertedConverter}}">
                                        <Border.Style>
                                            <Style TargetType="Border">
                                                <Setter Property="Background" Value="{StaticResource Brush.Text.Muted}" />
                                                <Style.Triggers>
                                                    <DataTrigger Binding="{Binding IsCompleted}" Value="True">
                                                        <Setter Property="Background" Value="{StaticResource Brush.Mint.Muted}" />
                                                    </DataTrigger>
                                                    <DataTrigger Binding="{Binding IsActive}" Value="True">
                                                        <Setter Property="Background" Value="{StaticResource Brush.Mint.Muted}" />
                                                    </DataTrigger>
                                                </Style.Triggers>
                                            </Style>
                                        </Border.Style>
                                    </Border>

                                    <!--  Pill dot — expands when active  -->
                                    <Border Height="8" CornerRadius="4">
                                        <Border.Style>
                                            <Style TargetType="Border">
                                                <Setter Property="Width" Value="8" />
                                                <Setter Property="Background" Value="{StaticResource Brush.Text.Muted}" />
                                                <Style.Triggers>
                                                    <DataTrigger Binding="{Binding IsCompleted}" Value="True">
                                                        <Setter Property="Background" Value="{StaticResource Brush.Mint.Muted}" />
                                                    </DataTrigger>
                                                    <DataTrigger Binding="{Binding IsActive}" Value="True">
                                                        <Setter Property="Background" Value="{StaticResource Brush.Mint}" />
                                                        <DataTrigger.EnterActions>
                                                            <BeginStoryboard>
                                                                <Storyboard>
                                                                    <DoubleAnimation
                                                                        Storyboard.TargetProperty="Width"
                                                                        To="24"
                                                                        Duration="0:0:0.2">
                                                                        <DoubleAnimation.EasingFunction>
                                                                            <CubicEase EasingMode="EaseOut" />
                                                                        </DoubleAnimation.EasingFunction>
                                                                    </DoubleAnimation>
                                                                </Storyboard>
                                                            </BeginStoryboard>
                                                        </DataTrigger.EnterActions>
                                                        <DataTrigger.ExitActions>
                                                            <BeginStoryboard>
                                                                <Storyboard>
                                                                    <DoubleAnimation
                                                                        Storyboard.TargetProperty="Width"
                                                                        To="8"
                                                                        Duration="0:0:0.2">
                                                                        <DoubleAnimation.EasingFunction>
                                                                            <CubicEase EasingMode="EaseOut" />
                                                                        </DoubleAnimation.EasingFunction>
                                                                    </DoubleAnimation>
                                                                </Storyboard>
                                                            </BeginStoryboard>
                                                        </DataTrigger.ExitActions>
                                                    </DataTrigger>
                                                </Style.Triggers>
                                            </Style>
                                        </Border.Style>
                                    </Border>

                                </StackPanel>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>

</ResourceDictionary>
```

- [ ] **Step 2: Commit**

```bash
git add Fluxo/Resources/Styles/StepNavigatorStyle.xaml
git commit -m "feat: add StepNavigatorStyle control template"
```

---

## Task 5: Register style in App.xaml

**Files:**
- Modify: `Fluxo/App.xaml:71-72`

- [ ] **Step 1: Add the merge after SettingsStyle.xaml (line 71)**

Find this block:
```xml
                <ResourceDictionary Source="Resources/Styles/SettingsStyle.xaml" />
                <ResourceDictionary Source="Resources/Styles/StartupWizardStyle.xaml" />
```

Replace with:
```xml
                <ResourceDictionary Source="Resources/Styles/SettingsStyle.xaml" />
                <ResourceDictionary Source="Resources/Styles/StepNavigatorStyle.xaml" />
                <ResourceDictionary Source="Resources/Styles/StartupWizardStyle.xaml" />
```

- [ ] **Step 2: Build to verify no XAML errors**

```bash
dotnet build Fluxo/Fluxo.csproj 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add Fluxo/App.xaml
git commit -m "feat: register StepNavigatorStyle in App.xaml"
```

---

## Task 6: Migrate StartupWizardVM

**Files:**
- Modify: `Fluxo/ViewModels/Shell/StartupWizard/StartupWizardVM.cs`

The current VM (around line 42-43, 58, 86-87) initialises `StepDots` with `StartupWizardStepDotVM` instances and updates them in `OnCurrentStepIndexChanged`. Replace with a `CurrentStep` computed property.

- [ ] **Step 1: Remove StepDots and add CurrentStep**

Remove this line from the constructor (around line 42-43):
```csharp
        for (var i = 0; i < TotalSteps; i++)
            StepDots.Add(new StartupWizardStepDotVM(i, i == 0));
```

Remove the `StepDots` property (line 58):
```csharp
    public ObservableCollection<StartupWizardStepDotVM> StepDots { get; } = [];
```

Remove the dot-update loop in `OnCurrentStepIndexChanged` (lines 86-87):
```csharp
        foreach (var dot in StepDots)
            dot.IsActive = dot.StepIndex == value;
```

Add `CurrentStep` property and notify it after the existing `OnPropertyChanged` calls in `OnCurrentStepIndexChanged`:
```csharp
    public int CurrentStep => CurrentStepIndex + 1;
```

In `OnCurrentStepIndexChanged`, add after the existing `OnPropertyChanged` calls:
```csharp
        OnPropertyChanged(nameof(CurrentStep));
```

Also remove `using System.Collections.ObjectModel;` from the top of the file — it was only needed for `StepDots` and will be unused after this change. (`StartupWizardStepDotVM` is in the same namespace so there is no separate `using` for it to remove.)

The final `OnCurrentStepIndexChanged` should look like:
```csharp
    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsGreetingStep));
        OnPropertyChanged(nameof(IsNameStep));
        OnPropertyChanged(nameof(IsMiddleStep));
        OnPropertyChanged(nameof(IsLoadingStep));
        OnPropertyChanged(nameof(IsFinalStep));
        OnPropertyChanged(nameof(IsStep2Active));
        OnPropertyChanged(nameof(IsNextEnabled));
        OnPropertyChanged(nameof(CurrentStep));

        if (value is >= 2 and <= 7)
            MiddlePage.SetCurrentStepIndex(value);
    }
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build Fluxo/Fluxo.csproj 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 3: Run existing wizard tests**

```bash
dotnet test Fluxo.Tests --filter "StartupWizardVMTests" 2>&1 | tail -10
```

Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add Fluxo/ViewModels/Shell/StartupWizard/StartupWizardVM.cs
git commit -m "feat: replace StepDots with CurrentStep int property in StartupWizardVM"
```

---

## Task 7: Migrate StartupWizardPopup

**Files:**
- Modify: `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml:85-106`
- Modify: `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml.cs`

- [ ] **Step 1: Replace the ItemsControl navigator in XAML**

Find this block (lines 85–106):
```xml
            <!--  Dot Navigator  -->
            <ItemsControl
                Grid.Column="1"
                VerticalAlignment="Center"
                ItemsSource="{Binding StepDots}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <StackPanel Orientation="Horizontal" />
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Ellipse
                            Width="8"
                            Height="8"
                            Margin="3,0"
                            Cursor="Hand"
                            MouseLeftButtonDown="OnDotClick"
                            Style="{StaticResource WizardStepDotStyle}" />
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
```

Replace with:
```xml
            <!--  Dot Navigator  -->
            <customControls:StepNavigatorControl
                Grid.Column="1"
                VerticalAlignment="Center"
                CurrentStep="{Binding CurrentStep}"
                StepCount="{Binding TotalSteps}" />
```

- [ ] **Step 2: Remove OnDotClick from code-behind**

In `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml.cs`, remove:

The `using` alias at the top:
```csharp
using WizardStepDotVM = Fluxo.ViewModels.Shell.StartupWizard.StartupWizardStepDotVM;
```

The entire `OnDotClick` method (lines 261–274):
```csharp
    private async void OnDotClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WizardStepDotVM dot })
            return;

        var targetStep = dot.StepIndex;
        if (targetStep == _viewModel.CurrentStepIndex)
            return;

        if ((targetStep == 3 || targetStep == 4) && !_viewModel.HasSpendingSources)
            return;

        await AnimateStepTransitionAsync(() => _viewModel.NavigateToStep(targetStep));
    }
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build Fluxo/Fluxo.csproj 2>&1 | tail -5
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml.cs
git commit -m "feat: replace wizard ItemsControl navigator with StepNavigatorControl"
```

---

## Task 8: Cleanup

**Files:**
- Modify: `Fluxo/Resources/Styles/StartupWizardStyle.xaml:97-104`
- Delete: `Fluxo/ViewModels/Shell/StartupWizard/StartupWizardStepDotVM.cs`

- [ ] **Step 1: Remove WizardStepDotStyle from StartupWizardStyle.xaml**

Find and delete this block (lines 97–104):
```xml
    <Style x:Key="WizardStepDotStyle" TargetType="Ellipse">
        <Setter Property="Fill" Value="{StaticResource Brush.Text.Muted}" />
        <Style.Triggers>
            <DataTrigger Binding="{Binding IsActive}" Value="True">
                <Setter Property="Fill" Value="{StaticResource Brush.Mint}" />
            </DataTrigger>
        </Style.Triggers>
    </Style>
```

- [ ] **Step 2: Delete StartupWizardStepDotVM.cs**

```bash
git rm Fluxo/ViewModels/Shell/StartupWizard/StartupWizardStepDotVM.cs
```

- [ ] **Step 3: Build and run all tests**

```bash
dotnet build Fluxo.sln 2>&1 | tail -5
dotnet test Fluxo.Tests 2>&1 | tail -10
```

Expected: build succeeds, all tests pass.

- [ ] **Step 4: Final commit**

```bash
git add Fluxo/Resources/Styles/StartupWizardStyle.xaml
git commit -m "chore: remove superseded WizardStepDotStyle and StartupWizardStepDotVM"
```
