# BasePopup Host Blur & Dim Design

**Date:** 2026-04-13

## Goal

When a `BasePopup` opens a child popup, the parent popup should be blurred and dimmed — just as `MainWindow` is when it opens a popup. The effect applies to the entire popup interior (header + content), leaving the border and background unaffected.

---

## Section 1: `IPopupHost` Interface

**File:** `Fluxo/Resources/CustomControls/IPopupHost.cs`

```csharp
public interface IPopupHost
{
    void ShowPopupOverlay();
    void HidePopupOverlay();
}
```

`MainWindow` already has `ShowPopupOverlay()` and `HidePopupOverlay()` — add `: IPopupHost` to its class declaration with no logic changes.

---

## Section 2: `BasePopup` XAML Template

**File:** `Fluxo/Resources/Styles/PopupStyles.xaml`

The inner `Grid` (currently unnamed) is given the name `PART_ContentRoot`. A new `Rectangle` named `PART_PopupOverlay` is added as the last child of that Grid, spanning all rows.

```xml
<Border CornerRadius="16" ...>
  <Grid x:Name="PART_ContentRoot">
    <Grid.RowDefinitions>
      <RowDefinition Height="60" />
      <RowDefinition Height="*" />
    </Grid.RowDefinitions>

    <!-- header row — unchanged -->
    <!-- ContentPresenter row — unchanged -->

    <Rectangle
      x:Name="PART_PopupOverlay"
      Grid.RowSpan="2"
      Fill="#80000000"
      Opacity="0"
      Visibility="Collapsed"
      IsHitTestVisible="False" />
  </Grid>
</Border>
```

- `IsHitTestVisible="False"` — the overlay does not block mouse interaction with underlying popup UI.
- `Fill="#80000000"` — semi-transparent dark fill; opacity is driven by animation, not this alpha value.
- Starts as `Collapsed` + `Opacity="0"` to avoid any flicker.

---

## Section 3: `BasePopup.cs` Changes

**File:** `Fluxo/Resources/CustomControls/BasePopup.cs`

### 3a. Class declaration

```csharp
public class BasePopup : Window, IPopupHost
```

### 3b. Fields

Replace:
```csharp
private MainWindow? _ownerWindow;
```
With:
```csharp
private IPopupHost? _popupHost;
private FrameworkElement? _contentRoot;
private UIElement? _popupOverlay;
```

### 3c. `OnApplyTemplate`

Wire the two new template parts:
```csharp
_contentRoot  = GetTemplateChild("PART_ContentRoot")  as FrameworkElement;
_popupOverlay = GetTemplateChild("PART_PopupOverlay") as UIElement;
```

### 3d. `OnLoaded` / `OnClosed`

```csharp
private void OnLoaded(object sender, RoutedEventArgs e)
{
    _popupHost = Owner as IPopupHost;
    _popupHost?.ShowPopupOverlay();
}

private void OnClosed(object? sender, EventArgs e)
{
    _popupHost?.HidePopupOverlay();
}
```

`StartupWizardPopup` opened during first-run (no `Owner`) resolves `_popupHost` to `null` and no-ops correctly.

### 3e. `ShowPopupOverlay` / `HidePopupOverlay`

Mirror `MainWindow`'s existing logic, targeting `_contentRoot` (blur) and `_popupOverlay` (fade):

```csharp
public void ShowPopupOverlay()
{
    if (_contentRoot is null || _popupOverlay is null) return;

    _contentRoot.Effect = new BlurEffect { Radius = 20, RenderingBias = RenderingBias.Performance };
    _popupOverlay.Visibility = Visibility.Visible;

    var fadeIn = new DoubleAnimation(0, 0.5, TimeSpan.FromMilliseconds(OverlayAnimDuration))
    {
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
    };
    _popupOverlay.BeginAnimation(OpacityProperty, fadeIn);
}

public void HidePopupOverlay()
{
    if (_contentRoot is null || _popupOverlay is null) return;

    _contentRoot.Effect = null;

    var fadeOut = new DoubleAnimation(0.5, 0, TimeSpan.FromMilliseconds(OverlayAnimDuration))
    {
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
    };
    fadeOut.Completed += (_, _) =>
    {
        _popupOverlay.BeginAnimation(OpacityProperty, null);
        _popupOverlay.Opacity = 0;
        _popupOverlay.Visibility = Visibility.Collapsed;
    };
    _popupOverlay.BeginAnimation(OpacityProperty, fadeOut);
}
```

---

## Out of Scope

- `SpendingSourcesListPopup.OnSourceClick` casts `Owner as MainWindow` to delegate reopening to `MainWindow.OpenSpendingSourceDetailPopup()`. This is unrelated to blur/dim and remains unchanged.
- `StartupLoaderPopup` extends `Window` directly — not a `BasePopup`, not affected.
- All existing `{ Owner = this }` assignments at call sites are already correct and require no changes.

---

## Files Changed

| File | Change |
|------|--------|
| `Fluxo/Resources/CustomControls/IPopupHost.cs` | New file |
| `Fluxo/Views/Shell/MainWindow.xaml.cs` | Add `: IPopupHost` to class declaration |
| `Fluxo/Resources/Styles/PopupStyles.xaml` | Name inner Grid `PART_ContentRoot`; add `PART_PopupOverlay` Rectangle |
| `Fluxo/Resources/CustomControls/BasePopup.cs` | Implement `IPopupHost`; replace `_ownerWindow`; wire template parts; add overlay methods |
