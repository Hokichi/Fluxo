# Step Navigator Control — Design Spec

**Date:** 2026-04-20  
**Status:** Approved

---

## Overview

A reusable `StepNavigatorControl` custom control that renders a horizontal row of pill-shaped dots connected by lines, indicating progress through a multi-step flow. The current step is a lengthened pill; completed steps and upcoming steps are circular. Navigation between steps triggers an expand/contract animation on the active dot.

---

## Architecture

### New Files

| File | Purpose |
|---|---|
| `Fluxo/Views/CustomControls/StepNavigatorControl.cs` | Custom control (`Control` subclass) with dependency properties and dot state logic |
| `Fluxo/ViewModels/CustomControls/StepNavigatorDotVM.cs` | Lightweight observable VM per dot: `IsActive`, `IsCompleted`, `IsFirst` |
| `Fluxo/Resources/Styles/StepNavigatorStyle.xaml` | Control template, DataTrigger animations, connector and pill styles |

### Modified Files

| File | Change |
|---|---|
| `App.xaml` | Merge `StepNavigatorStyle.xaml` into global resources |
| `Fluxo/Views/Shell/Wizard/StartupWizardPopup.xaml` | Replace `ItemsControl` dot navigator + remove `OnDotClick` handler; add `<StepNavigatorControl>` |
| `Fluxo/ViewModels/Shell/StartupWizardVM.cs` | Remove `StepDots` collection and its management; expose `CurrentStep` (int) and `StepCount` (int) |

### Deleted Files

| File | Reason |
|---|---|
| `Fluxo/ViewModels/Shell/StartupWizard/StartupWizardStepDotVM.cs` | Superseded by `StepNavigatorDotVM` |

`WizardStepDotStyle` is removed from `StartupWizardStyle.xaml`.

---

## Control API

```csharp
public sealed class StepNavigatorControl : Control
{
    static StepNavigatorControl()
    {
        // Tells WPF to look up the default style in StepNavigatorStyle.xaml
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(StepNavigatorControl),
            new FrameworkPropertyMetadata(typeof(StepNavigatorControl)));
    }

    // Total number of steps to render
    public static readonly DependencyProperty StepCountProperty;
    public int StepCount { get; set; }

    // 1-based index of the currently active step
    public static readonly DependencyProperty CurrentStepProperty;
    public int CurrentStep { get; set; }

    // Read-only DP (RegisterReadOnly) so the control template can reach it via TemplateBinding
    public static readonly DependencyPropertyKey DotsPropertyKey;
    public static readonly DependencyProperty DotsProperty;
    public ObservableCollection<StepNavigatorDotVM> Dots { get; } // set via DotsPropertyKey
}
```

**Usage in any view:**
```xaml
<customControls:StepNavigatorControl
    CurrentStep="{Binding CurrentStep}"
    StepCount="{Binding StepCount}" />
```

The host VM only needs two int properties. No awareness of dots, animation, or brushes.

---

## Dot State VM

```csharp
public sealed partial class StepNavigatorDotVM : ObservableObject
{
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isCompleted;
    public bool IsFirst { get; init; }
}
```

---

## Data Flow

### StepCount changes → rebuild Dots

`OnStepCountChanged` tears down and rebuilds the `Dots` collection:
1. Clear existing items
2. Create `StepCount` new `StepNavigatorDotVM` instances; set `IsFirst = true` on index 0
3. Call `UpdateDotStates()`

### CurrentStep changes → update states in-place

`OnCurrentStepChanged` calls `UpdateDotStates()` without rebuilding:

```
foreach dot at index i:
    IsActive    = (i == CurrentStep - 1)
    IsCompleted = (i < CurrentStep - 1)
```

`PropertyChanged` on the VM fires, DataTriggers in the template react, animations play.

---

## Visual Design

### Dot

A `Border` with `CornerRadius="4"` (half of `Height=8`), forming a circle at default width and a pill when expanded.

| State | Width | Height | Background |
|---|---|---|---|
| Upcoming | 8 | 8 | `Brush.Text.Muted` |
| Completed | 8 | 8 | `Brush.Mint.Muted` |
| Active | 24 | 8 | `Brush.Mint` |

### Connector

A `Border` placed to the left of each dot. Hidden (`Visibility=Collapsed`) when `IsFirst=true`.

| Connector owner | Color |
|---|---|
| Active or Completed step | `Brush.Mint.Muted` |
| Upcoming step | `Brush.Text.Muted` |

Fixed size: `Width=8`, `Height=2`. Color is a `Setter`-only change (not animated).

### Dot spacing

Each item is a horizontal `StackPanel`: `[Connector] [Dot]`. The `ItemsPanel` is a horizontal `StackPanel` with `VerticalAlignment=Center`. No explicit `Margin` needed — the connector provides the gap.

---

## Animation

Triggered by `DataTrigger` on `IsActive` using `EnterActions` / `ExitActions`.

**Width animation** — `DataTrigger` on `IsActive` using `EnterActions` / `ExitActions`:

| Property | Enter (IsActive → True) | Exit (IsActive → False) |
|---|---|---|
| `Border.Width` | `8 → 24` | `24 → 8` |

- **Duration:** 200ms, **Easing:** `CubicEase EaseOut`

**Color animation** — separate `DataTrigger`s for `IsActive` and `IsCompleted`, each with their own `ColorAnimation` via `EnterActions`. This keeps exit color correct regardless of nav direction (forward → becomes completed = Mint.Muted; backward → becomes upcoming = Text.Muted):

| Trigger | Target color |
|---|---|
| `IsActive = True` | `Color.Mint` |
| `IsCompleted = True` | `Color.Mint.Muted` |
| Both false (upcoming) | `Color.Text.Muted` (baseline `Setter`) |

The `ColorAnimation` targets the `Color` property of a `SolidColorBrush` defined locally on the `Border`, matching the pattern used in `BalloonButton.cs`.

The connector color change uses a plain `Setter` — not animated.

---

## Migration: StartupWizard

`StartupWizardVM` currently exposes:
- `StepDots` — `ObservableCollection<StartupWizardStepDotVM>` (removed)
- Manual `IsActive` updates on each dot (removed)

Replace with:
- `StepCount` — set once to `StartupWizardShared.TotalSteps` (currently 10)
- `CurrentStep` — incremented/decremented in existing `GoNextAsync()` / `GoBack()` methods

`OnDotClick` handler in `StartupWizardPopup.xaml.cs` is removed (dots are non-interactive).

---

## Non-Goals

- Dots are not clickable — no `IsHitTestVisible`, no click events
- No label text on dots
- No vertical orientation
- No custom dot size configuration (fixed 8px height)
