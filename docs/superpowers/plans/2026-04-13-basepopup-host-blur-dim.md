# BasePopup Host Blur & Dim Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When a `BasePopup` opens a child popup, the parent popup blurs and dims (just as `MainWindow` does), blocking interaction until the child closes.

**Architecture:** Introduce an `IPopupHost` interface with `ShowPopupOverlay()`/`HidePopupOverlay()`. Both `MainWindow` and `BasePopup` implement it. `BasePopup.OnLoaded` casts `Owner as IPopupHost` and calls the interface — no more hard-coded `MainWindow` reference. `BasePopup`'s XAML template gains a named inner Grid (`PART_ContentRoot`) for blur and a `Rectangle` (`PART_PopupOverlay`) for the dim overlay.

**Tech Stack:** C# 12, WPF (.NET 8), no test project exists — verification is build + manual run.

---

## File Map

| File | Action |
|------|--------|
| `Fluxo/Resources/CustomControls/IPopupHost.cs` | **Create** — interface with two methods |
| `Fluxo/Views/Shell/MainWindow.xaml.cs` | **Modify** — add `: IPopupHost` to class declaration |
| `Fluxo/Resources/Styles/PopupStyles.xaml` | **Modify** — name inner Grid; add dim overlay Rectangle |
| `Fluxo/Resources/CustomControls/BasePopup.cs` | **Modify** — implement interface, rewire fields and template parts, add overlay methods |

---

### Task 1: Create `IPopupHost` interface

**Files:**
- Create: `Fluxo/Resources/CustomControls/IPopupHost.cs`

- [ ] **Step 1: Create the file**

```csharp
namespace Fluxo.Resources.CustomControls;

public interface IPopupHost
{
    void ShowPopupOverlay();
    void HidePopupOverlay();
}
```

- [ ] **Step 2: Build to confirm no errors**

```
dotnet build Fluxo/Fluxo.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fluxo/Resources/CustomControls/IPopupHost.cs
git commit -m "feat: add IPopupHost interface"
```

---

### Task 2: `MainWindow` implements `IPopupHost`

**Files:**
- Modify: `Fluxo/Views/Shell/MainWindow.xaml.cs:25`

`MainWindow` already has `ShowPopupOverlay()` and `HidePopupOverlay()` — only the class declaration changes.

- [ ] **Step 1: Add `: IPopupHost` to the class declaration**

Change line 25 from:
```csharp
public partial class MainWindow : Window
```
To:
```csharp
public partial class MainWindow : Window, IPopupHost
```

Add the using at the top of the file alongside the existing usings:
```csharp
using Fluxo.Resources.CustomControls;
```

- [ ] **Step 2: Build to confirm no errors**

```
dotnet build Fluxo/Fluxo.csproj
```

Expected: Build succeeded, 0 errors. The two existing methods already satisfy the interface contract.

- [ ] **Step 3: Commit**

```bash
git add Fluxo/Views/Shell/MainWindow.xaml.cs
git commit -m "feat: MainWindow implements IPopupHost"
```

---

### Task 3: Add overlay elements to `BasePopup` XAML template

**Files:**
- Modify: `Fluxo/Resources/Styles/PopupStyles.xaml`

The inner `Grid` (currently unnamed, direct child of the root `Border`) gets named `PART_ContentRoot`. A new `Rectangle` named `PART_PopupOverlay` is appended as the last child of that Grid, spanning all rows.

- [ ] **Step 1: Name the inner Grid and add the overlay Rectangle**

Locate the `Style TargetType="{x:Type c:BasePopup}"` block. Inside the `ControlTemplate`, find the `<Grid>` that is the direct child of the root `<Border>`. Change it from:

```xml
<Grid>
    <Grid.RowDefinitions>
        <RowDefinition Height="60" />
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>
```

To:

```xml
<Grid x:Name="PART_ContentRoot">
    <Grid.RowDefinitions>
        <RowDefinition Height="60" />
        <RowDefinition Height="*" />
    </Grid.RowDefinitions>
```

Then, immediately before the closing `</Grid>` tag (after the `<ContentPresenter Grid.Row="1" />`), add:

```xml
<!--  Popup overlay: dims and blocks interaction when a child popup is open  -->
<Rectangle
    x:Name="PART_PopupOverlay"
    Grid.RowSpan="2"
    Fill="#80000000"
    Opacity="0"
    Visibility="Collapsed" />
```

- [ ] **Step 2: Build to confirm no XAML errors**

```
dotnet build Fluxo/Fluxo.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Fluxo/Resources/Styles/PopupStyles.xaml
git commit -m "feat: add PART_ContentRoot and PART_PopupOverlay to BasePopup template"
```

---

### Task 4: `BasePopup` implements `IPopupHost`

**Files:**
- Modify: `Fluxo/Resources/CustomControls/BasePopup.cs`

This task replaces the `MainWindow`-specific overlay logic with the `IPopupHost`-based approach and adds `ShowPopupOverlay`/`HidePopupOverlay` implementations so `BasePopup` can itself act as a host.

- [ ] **Step 1: Update the using directives**

Remove:
```csharp
using Fluxo.Views.Shell;
```

Add (if not already present — `Views.Shell` import was only needed for `MainWindow`):
```csharp
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
```

> `System.Windows.Media.Animation` and `System.Windows.Media.Effects` are needed for `DoubleAnimation`, `CubicEase`, and `BlurEffect`. Check the existing usings first — some may already be present.

- [ ] **Step 2: Update the class declaration**

Change:
```csharp
public class BasePopup : Window
```
To:
```csharp
public class BasePopup : Window, IPopupHost
```

- [ ] **Step 3: Replace the `_ownerWindow` field with the three new fields**

Remove:
```csharp
private MainWindow? _ownerWindow;
```

Add in its place:
```csharp
private IPopupHost? _popupHost;
private FrameworkElement? _contentRoot;
private UIElement? _popupOverlay;
```

- [ ] **Step 4: Wire template parts in `OnApplyTemplate`**

At the end of the existing `OnApplyTemplate` override, after the last `WireButton(...)` call, add:

```csharp
_contentRoot  = GetTemplateChild("PART_ContentRoot")  as FrameworkElement;
_popupOverlay = GetTemplateChild("PART_PopupOverlay") as UIElement;
```

The full method should look like:

```csharp
public override void OnApplyTemplate()
{
    base.OnApplyTemplate();

    WireButton("PART_CloseButton",            _ => OnCloseButtonClick());
    WireButton("PART_SaveButton",             _ => OnSaveButtonClick());
    WireButton("PART_SaveAndCreateNewButton", _ => OnSaveAndCreateNewButtonClick());
    WireButton("PART_ApplyButton",            _ => OnApplyButtonClick());
    WireButton("PART_RevertButton",           _ => OnRevertButtonClick());
    WireButton("PART_EditButton",             _ => OnEditButtonClick());
    WireButton("PART_DeleteButton",           _ => OnDeleteButtonClick());
    WireButton("PART_CloneButton",            _ => OnCloneButtonClick());
    WireButton("PART_CancelButton",           _ => OnCancelButtonClick());

    _contentRoot  = GetTemplateChild("PART_ContentRoot")  as FrameworkElement;
    _popupOverlay = GetTemplateChild("PART_PopupOverlay") as UIElement;
}
```

- [ ] **Step 5: Update `OnLoaded` and `OnClosed`**

Replace:
```csharp
private void OnLoaded(object sender, RoutedEventArgs e)
{
    _ownerWindow = Owner as MainWindow;
    _ownerWindow?.ShowPopupOverlay();
}

private void OnClosed(object? sender, EventArgs e)
{
    _ownerWindow?.HidePopupOverlay();
}
```

With:
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

- [ ] **Step 6: Add `ShowPopupOverlay` and `HidePopupOverlay`**

Replace the `// ── Overlay & blur on MainWindow ──` comment block (which currently contains only `OnLoaded` and `OnClosed`) — keep those two methods and add the new public methods after them:

```csharp
// ── Overlay & blur on owner ─────────────────────────────────────

private void OnLoaded(object sender, RoutedEventArgs e)
{
    _popupHost = Owner as IPopupHost;
    _popupHost?.ShowPopupOverlay();
}

private void OnClosed(object? sender, EventArgs e)
{
    _popupHost?.HidePopupOverlay();
}

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

- [ ] **Step 7: Build to confirm no errors**

```
dotnet build Fluxo/Fluxo.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Run the app and verify the effect**

1. Open the app → open **Settings** (Ctrl+S). SettingsPopup appears; MainWindow blurs/dims as before.
2. Inside Settings, click **Add New Spending Source**. SettingsPopup should blur and dim. AddSpendingSourcePopup opens on top.
3. Close AddSpendingSourcePopup. SettingsPopup should un-blur and restore.
4. Repeat with **Add Tag** and **Delete All Data** buttons in Settings to cover other sub-popup paths.
5. Also verify **StartupWizardPopup** sub-popups (Add Spending Source / Fixed Expense / Goal) work the same way.

- [ ] **Step 9: Commit**

```bash
git add Fluxo/Resources/CustomControls/BasePopup.cs
git commit -m "feat: BasePopup implements IPopupHost, blur/dims owner on open"
```
