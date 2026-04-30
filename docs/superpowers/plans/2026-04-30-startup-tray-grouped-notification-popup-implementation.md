# Startup Tray Grouped Notification Popup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show a one-time custom startup tray popup (autostart path only) that summarizes grouped notifications with category-aware single-group messaging, a restore button, a close button, and 5-second auto-dismiss.

**Architecture:** Add a dedicated startup summary service that reads persisted notifications, groups them through the existing notification grouping service, and formats popup text according to the approved rules. Add a dedicated `StartupNotificationPopup` window for UI/interaction, then wire `App.xaml.cs` tray-autostart flow to show this popup once per process when summary data is valid. Keep tray menu behavior unchanged.

**Tech Stack:** C# (.NET/WPF), CommunityToolkit.Mvvm (existing), xUnit, NSubstitute

---

## File Structure

- Create: `Fluxo/Services/Notifications/StartupNotificationSummary.cs`
- Create: `Fluxo/Services/Notifications/IStartupNotificationSummaryService.cs`
- Create: `Fluxo/Services/Notifications/StartupNotificationSummaryService.cs`
- Create: `Fluxo/Views/Shell/Tray/StartupNotificationPopup.xaml`
- Create: `Fluxo/Views/Shell/Tray/StartupNotificationPopup.xaml.cs`
- Create: `Fluxo/Services/Ui/StartupTrayPopupDisplayPolicy.cs`
- Modify: `Fluxo/Extensions/ServiceCollectionExtensions.cs`
- Modify: `Fluxo/App.xaml.cs`
- Create: `Fluxo.Tests/Services/Notifications/StartupNotificationSummaryServiceTests.cs`
- Create: `Fluxo.Tests/Views/Shell/Tray/StartupNotificationPopupLayoutTests.cs`
- Create: `Fluxo.Tests/Services/Ui/StartupTrayPopupDisplayPolicyTests.cs`

## Task 1: Build Startup Notification Summary Domain Service

**Files:**
- Create: `Fluxo/Services/Notifications/StartupNotificationSummary.cs`
- Create: `Fluxo/Services/Notifications/IStartupNotificationSummaryService.cs`
- Create: `Fluxo/Services/Notifications/StartupNotificationSummaryService.cs`
- Test: `Fluxo.Tests/Services/Notifications/StartupNotificationSummaryServiceTests.cs`

- [ ] **Step 1: Write failing summary-service tests for messaging rules**

```csharp
using Fluxo.Core.Entities;
using Fluxo.Core.Interfaces;
using Fluxo.Services.Notifications;
using Fluxo.Tests.TestDoubles;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Services.Notifications;

public sealed class StartupNotificationSummaryServiceTests
{
    [Fact]
    public async Task BuildAsync_SingleFixedExpenseGroupWithOneItem_UsesExpenseNameMessage()
    {
        var sut = CreateSut(
        [
            new Notification
            {
                Id = 1,
                Type = "UpcomingDeduction-44_20260501",
                Header = "Upcoming Deduction - Rent",
                Message = "Rent is scheduled for May 1.",
                CreatedOn = DateTime.Today
            }
        ]);

        var summary = await sut.BuildAsync();

        Assert.NotNull(summary);
        Assert.Equal(1, summary!.GroupCount);
        Assert.Equal("Rent is due", summary.Message);
    }

    [Fact]
    public async Task BuildAsync_SingleUpcomingPaymentGroupWithOneItem_UsesCardNameMessage()
    {
        var sut = CreateSut(
        [
            new Notification
            {
                Id = 1,
                Type = "UpcomingPayment-10_20260501",
                Header = "Upcoming Payment - Visa",
                Message = "Visa is due on May 1.",
                CreatedOn = DateTime.Today
            }
        ]);

        var summary = await sut.BuildAsync();

        Assert.NotNull(summary);
        Assert.Equal("Visa is due", summary!.Message);
    }

    [Fact]
    public async Task BuildAsync_SingleGoalDeadlineGroupWithOneItem_UsesGoalNameMessage()
    {
        var sut = CreateSut(
        [
            new Notification
            {
                Id = 1,
                Type = "GoalDeadline-9_20260501",
                Header = "Goal Deadline - Emergency Fund",
                Message = "Emergency Fund ends on May 1 (1 days left).",
                CreatedOn = DateTime.Today
            }
        ]);

        var summary = await sut.BuildAsync();

        Assert.NotNull(summary);
        Assert.Equal("Goal Emergency Fund is reaching its deadline", summary!.Message);
    }

    [Fact]
    public async Task BuildAsync_MultipleGroups_UsesGroupCountMessage()
    {
        var sut = CreateSut(
        [
            new Notification
            {
                Id = 1,
                Type = "UpcomingPayment-10_20260501",
                Header = "Upcoming Payment - Visa",
                Message = "Visa is due on May 1.",
                CreatedOn = DateTime.Today
            },
            new Notification
            {
                Id = 2,
                Type = "LowBalance-5",
                Header = "Low Balance - Wallet",
                Message = "Wallet is down to 20%.",
                CreatedOn = DateTime.Today.AddMinutes(-1)
            }
        ]);

        var summary = await sut.BuildAsync();

        Assert.NotNull(summary);
        Assert.Equal(2, summary!.GroupCount);
        Assert.Equal("There are 2 notifications", summary.Message);
    }

    [Fact]
    public async Task BuildAsync_SingleUnknownCategoryWithOneItem_UsesPrimaryHeader()
    {
        var sut = CreateSut(
        [
            new Notification
            {
                Id = 1,
                Type = "CustomType-1",
                Header = "Custom Header",
                Message = "Custom body",
                CreatedOn = DateTime.Today
            }
        ]);

        var summary = await sut.BuildAsync();

        Assert.NotNull(summary);
        Assert.Equal("Custom Header", summary!.Message);
    }

    [Fact]
    public async Task BuildAsync_WhenRepositoryThrows_ReturnsNull()
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Notifications.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<Notification>>>(_ => throw new InvalidOperationException("boom"));

        var runner = new InlineDataOperationRunner(unitOfWork);
        var sut = new StartupNotificationSummaryService(runner, new NotificationGroupingService());

        var summary = await sut.BuildAsync();

        Assert.Null(summary);
    }

    private static StartupNotificationSummaryService CreateSut(IReadOnlyList<Notification> rows)
    {
        var unitOfWork = Substitute.For<IUnitOfWork>();
        unitOfWork.Notifications.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(rows);

        var runner = new InlineDataOperationRunner(unitOfWork);
        return new StartupNotificationSummaryService(runner, new NotificationGroupingService());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~StartupNotificationSummaryServiceTests" -v minimal`  
Expected: FAIL because startup summary types/service do not exist yet.

- [ ] **Step 3: Add startup summary DTO and service interface**

```csharp
// Fluxo/Services/Notifications/StartupNotificationSummary.cs
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Services.Notifications;

public sealed record StartupNotificationSummary(
    int GroupCount,
    NotificationGroupCategory PrimaryGroupCategory,
    int PrimaryGroupItemCount,
    string PrimaryHeader,
    string? PrimaryEntityName,
    string Message);

// Fluxo/Services/Notifications/IStartupNotificationSummaryService.cs
namespace Fluxo.Services.Notifications;

public interface IStartupNotificationSummaryService
{
    Task<StartupNotificationSummary?> BuildAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Implement startup summary service with approved formatting rules**

```csharp
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Operations;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell.Main;

namespace Fluxo.Services.Notifications;

public sealed class StartupNotificationSummaryService(
    IDataOperationRunner dataOperationRunner,
    INotificationGroupingService groupingService) : IStartupNotificationSummaryService
{
    public async Task<StartupNotificationSummary?> BuildAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var persisted = await dataOperationRunner.RunAsync(async (scope, ct) =>
                await scope.UnitOfWork.Notifications.GetActiveAsync(ct), cancellationToken);

            var visibleRows = persisted
                .Where(row => !row.IsForDeletion && !row.IsCleared)
                .OrderByDescending(row => row.CreatedOn)
                .ToList();

            if (visibleRows.Count == 0)
                return null;

            var grouped = groupingService.Group(visibleRows.Select(ToVm).ToList());
            if (grouped.Count == 0)
                return null;

            var primary = grouped[0];
            var message = BuildMessage(grouped, primary);
            if (string.IsNullOrWhiteSpace(message))
                return null;

            return new StartupNotificationSummary(
                GroupCount: grouped.Count,
                PrimaryGroupCategory: primary.Category,
                PrimaryGroupItemCount: primary.Count,
                PrimaryHeader: primary.Header,
                PrimaryEntityName: TryResolveEntityName(primary),
                Message: message);
        }
        catch
        {
            return null;
        }
    }

    private static NotificationVM ToVm(Notification row)
    {
        return new NotificationVM
        {
            Type = row.Type,
            Header = row.Header,
            Message = row.Message,
            CreatedOn = row.CreatedOn,
            IsCleared = row.IsCleared,
            Severity = InferSeverity(row.Type)
        };
    }

    private static string BuildMessage(IReadOnlyList<NotificationItemVM> grouped, NotificationItemVM primary)
    {
        if (grouped.Count > 1)
            return $"There are {grouped.Count} notifications";

        if (primary.Count <= 0)
            return string.Empty;

        return primary.Category switch
        {
            NotificationGroupCategory.FixedExpenseDue => primary.Count == 1
                ? $"{TryResolveEntityName(primary)} is due"
                : $"There are {primary.Count} fixed expenses due",
            NotificationGroupCategory.UpcomingPayment => primary.Count == 1
                ? $"{TryResolveEntityName(primary)} is due"
                : $"There are {primary.Count} credit cards due",
            NotificationGroupCategory.GoalDeadline => primary.Count == 1
                ? $"Goal {TryResolveEntityName(primary)} is reaching its deadline"
                : $"There are {primary.Count} goals reaching their deadlines",
            NotificationGroupCategory.LatePayment => primary.Count == 1
                ? "There is one late payment due"
                : $"There are {primary.Count} late payments due",
            _ => primary.Count == 1
                ? primary.Header
                : $"There are {primary.Count} notifications"
        };
    }

    private static string TryResolveEntityName(NotificationItemVM primary)
    {
        var header = primary.Notifications.FirstOrDefault()?.Header ?? primary.Header;
        var separatorIndex = header.IndexOf(" - ", StringComparison.Ordinal);
        if (separatorIndex < 0 || separatorIndex >= header.Length - 3)
            return header.Trim();

        return header[(separatorIndex + 3)..].Trim();
    }

    private static NotificationSeverity InferSeverity(string type)
    {
        var token = type.Split('_')[0];
        if (token.StartsWith("LatePayment", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("LowBalance", StringComparison.OrdinalIgnoreCase))
            return NotificationSeverity.Danger;

        if (token.StartsWith("AutoExpenseProcessed", StringComparison.OrdinalIgnoreCase))
            return NotificationSeverity.Success;

        if (token.StartsWith("UpcomingPayment", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("UpcomingDeduction", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("GoalDeadline", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("LowCredit", StringComparison.OrdinalIgnoreCase) ||
            token.StartsWith("BudgetThreshold", StringComparison.OrdinalIgnoreCase))
            return NotificationSeverity.Warning;

        return NotificationSeverity.Info;
    }
}
```

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~StartupNotificationSummaryServiceTests" -v minimal`  
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Fluxo/Services/Notifications/StartupNotificationSummary.cs Fluxo/Services/Notifications/IStartupNotificationSummaryService.cs Fluxo/Services/Notifications/StartupNotificationSummaryService.cs Fluxo.Tests/Services/Notifications/StartupNotificationSummaryServiceTests.cs
git commit -m "feat: add startup grouped notification summary service"
```

## Task 2: Add Startup Popup UI With Circular Actions and Auto-Close

**Files:**
- Create: `Fluxo/Views/Shell/Tray/StartupNotificationPopup.xaml`
- Create: `Fluxo/Views/Shell/Tray/StartupNotificationPopup.xaml.cs`
- Test: `Fluxo.Tests/Views/Shell/Tray/StartupNotificationPopupLayoutTests.cs`

- [ ] **Step 1: Write failing layout test for required popup elements**

```csharp
using System.Xml.Linq;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Tray;

public sealed class StartupNotificationPopupLayoutTests
{
    [Fact]
    public void Popup_UsesAngleRightActionButton_AndCloseActionButton()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "Fluxo",
            "Views",
            "Shell",
            "Tray",
            "StartupNotificationPopup.xaml"));

        var root = XElement.Load(xamlPath);
        var descendants = root.Descendants().ToList();

        Assert.Contains(descendants, node =>
            string.Equals(node.Name.LocalName, "Path", StringComparison.Ordinal) &&
            string.Equals((string?)node.Attribute("Data"), "{StaticResource AngleRight}", StringComparison.Ordinal));
        Assert.Contains(descendants, node =>
            string.Equals(node.Name.LocalName, "TextBlock", StringComparison.Ordinal) &&
            string.Equals((string?)node.Attribute("Text"), "Close", StringComparison.Ordinal));
        Assert.Contains(descendants, node =>
            string.Equals(node.Name.LocalName, "TextBlock", StringComparison.Ordinal) &&
            string.Equals((string?)node.Attribute("Name"), "SummaryTextBlock", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Popup_DeclaresCircularButtonStyle_AndUsesItForBothActionButtons()
    {
        var xamlPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "Fluxo",
            "Views",
            "Shell",
            "Tray",
            "StartupNotificationPopup.xaml"));
        var xaml = File.ReadAllText(xamlPath);

        Assert.Contains("x:Key=\"StartupPopupCircularButtonStyle\"", xaml);
        Assert.Contains("Style=\"{StaticResource StartupPopupCircularButtonStyle}\"", xaml);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~StartupNotificationPopupLayoutTests" -v minimal`  
Expected: FAIL because popup files do not exist.

- [ ] **Step 3: Create startup popup XAML with summary text and two circular right-side buttons**

```xml
<Window
    x:Class="Fluxo.Views.Shell.Tray.StartupNotificationPopup"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    AllowsTransparency="True"
    Background="Transparent"
    ShowInTaskbar="False"
    SizeToContent="WidthAndHeight"
    Topmost="True"
    WindowStyle="None">
    <Window.Resources>
        <Style x:Key="StartupPopupCircularButtonStyle" TargetType="Button">
            <Setter Property="Width" Value="42" />
            <Setter Property="Height" Value="42" />
            <Setter Property="Background" Value="{StaticResource Brush.Background.Surface}" />
            <Setter Property="BorderBrush" Value="{StaticResource Brush.Border.Subtle}" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border
                            x:Name="ButtonChrome"
                            Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="21">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="ButtonChrome" Property="Background" Value="{StaticResource Brush.Background.Hover}" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Border
        Margin="8"
        Padding="12"
        Background="{StaticResource Brush.Background.Surface}"
        BorderBrush="{StaticResource Brush.Border.Subtle}"
        BorderThickness="1"
        CornerRadius="12">
        <Grid Width="320">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*" />
                <ColumnDefinition Width="Auto" />
            </Grid.ColumnDefinitions>

            <TextBlock
                x:Name="SummaryTextBlock"
                Grid.Column="0"
                Margin="4,0,12,0"
                VerticalAlignment="Center"
                FontFamily="{StaticResource Medium}"
                FontSize="13"
                Foreground="{StaticResource Brush.Text.Primary}"
                TextWrapping="Wrap" />

            <StackPanel Grid.Column="1" Orientation="Vertical">
                <Button
                    x:Name="OpenAppButton"
                    Margin="0,0,0,8"
                    Click="OnOpenAppClick"
                    Style="{StaticResource StartupPopupCircularButtonStyle}">
                    <Path
                        Width="16"
                        Height="16"
                        Fill="{StaticResource Brush.Text.Primary}"
                        Stretch="Uniform"
                        Data="{StaticResource AngleRight}" />
                </Button>

                <Button
                    x:Name="ClosePopupButton"
                    Click="OnClosePopupClick"
                    Style="{StaticResource StartupPopupCircularButtonStyle}">
                    <TextBlock
                        FontFamily="{StaticResource Bold}"
                        FontSize="11"
                        Foreground="{StaticResource Brush.Text.Primary}"
                        Text="Close" />
                </Button>
            </StackPanel>
        </Grid>
    </Border>
</Window>
```

- [ ] **Step 4: Implement popup code-behind (events, placement, auto-dismiss timer)**

```csharp
using System.Windows;
using System.Windows.Threading;

namespace Fluxo.Views.Shell.Tray;

public partial class StartupNotificationPopup : Window
{
    private readonly DispatcherTimer _autoCloseTimer = new() { Interval = TimeSpan.FromSeconds(5) };

    public StartupNotificationPopup()
    {
        InitializeComponent();
        Deactivated += (_, _) => Hide();
        _autoCloseTimer.Tick += OnAutoCloseTick;
    }

    public event EventHandler? OpenAppRequested;
    public event EventHandler? ClosedByUserRequested;

    public void ShowNearScreenPoint(Point screenPoint, string summaryText)
    {
        SummaryTextBlock.Text = summaryText;
        _autoCloseTimer.Stop();

        const double horizontalPadding = 12;
        const double verticalPadding = 12;
        var width = Math.Max(ActualWidth, 340);
        var height = Math.Max(ActualHeight, 120);
        var workArea = SystemParameters.WorkArea;

        Left = Math.Clamp(screenPoint.X - width + horizontalPadding, workArea.Left, workArea.Right - width);
        Top = Math.Clamp(screenPoint.Y - height - verticalPadding, workArea.Top, workArea.Bottom - height);

        if (!IsVisible)
            Show();
        else
            Activate();

        _autoCloseTimer.Start();
    }

    private void OnOpenAppClick(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        Hide();
        OpenAppRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnClosePopupClick(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        Hide();
        ClosedByUserRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnAutoCloseTick(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
        Hide();
    }
}
```

- [ ] **Step 5: Run popup layout tests to verify pass**

Run: `dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~StartupNotificationPopupLayoutTests" -v minimal`  
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Fluxo/Views/Shell/Tray/StartupNotificationPopup.xaml Fluxo/Views/Shell/Tray/StartupNotificationPopup.xaml.cs Fluxo.Tests/Views/Shell/Tray/StartupNotificationPopupLayoutTests.cs
git commit -m "feat: add startup tray notification popup UI"
```

## Task 3: Add Startup Tray Popup Display Policy (Autostart-Only + Once-Per-Process)

**Files:**
- Create: `Fluxo/Services/Ui/StartupTrayPopupDisplayPolicy.cs`
- Test: `Fluxo.Tests/Services/Ui/StartupTrayPopupDisplayPolicyTests.cs`

- [ ] **Step 1: Write failing policy tests**

```csharp
using Fluxo.Services.Ui;
using Xunit;

namespace Fluxo.Tests.Services.Ui;

public sealed class StartupTrayPopupDisplayPolicyTests
{
    [Fact]
    public void ShouldShow_WhenNotTrayLaunch_ReturnsFalse()
    {
        var sut = new StartupTrayPopupDisplayPolicy();
        Assert.False(sut.ShouldShow(launchInTrayMode: false, alreadyShownThisProcess: false, hasSummary: true));
    }

    [Fact]
    public void ShouldShow_WhenAlreadyShownInProcess_ReturnsFalse()
    {
        var sut = new StartupTrayPopupDisplayPolicy();
        Assert.False(sut.ShouldShow(launchInTrayMode: true, alreadyShownThisProcess: true, hasSummary: true));
    }

    [Fact]
    public void ShouldShow_WhenNoSummary_ReturnsFalse()
    {
        var sut = new StartupTrayPopupDisplayPolicy();
        Assert.False(sut.ShouldShow(launchInTrayMode: true, alreadyShownThisProcess: false, hasSummary: false));
    }

    [Fact]
    public void ShouldShow_WhenTrayLaunchNotShownAndHasSummary_ReturnsTrue()
    {
        var sut = new StartupTrayPopupDisplayPolicy();
        Assert.True(sut.ShouldShow(launchInTrayMode: true, alreadyShownThisProcess: false, hasSummary: true));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~StartupTrayPopupDisplayPolicyTests" -v minimal`  
Expected: FAIL because policy type does not exist.

- [ ] **Step 3: Implement policy class**

```csharp
namespace Fluxo.Services.Ui;

public sealed class StartupTrayPopupDisplayPolicy
{
    public bool ShouldShow(bool launchInTrayMode, bool alreadyShownThisProcess, bool hasSummary)
    {
        return launchInTrayMode && !alreadyShownThisProcess && hasSummary;
    }
}
```

- [ ] **Step 4: Re-run policy tests**

Run: `dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~StartupTrayPopupDisplayPolicyTests" -v minimal`  
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Fluxo/Services/Ui/StartupTrayPopupDisplayPolicy.cs Fluxo.Tests/Services/Ui/StartupTrayPopupDisplayPolicyTests.cs
git commit -m "test: cover startup tray popup display gating policy"
```

## Task 4: Integrate Startup Summary + Popup Into App Tray Autostart Flow

**Files:**
- Modify: `Fluxo/Extensions/ServiceCollectionExtensions.cs`
- Modify: `Fluxo/App.xaml.cs`
- Create: `Fluxo.Tests/Extensions/ServiceCollectionExtensionsTests.cs`

- [ ] **Step 1: Write failing DI registration test for startup summary service**

```csharp
using Fluxo.Extensions;
using Fluxo.Services.Notifications;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fluxo.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUIData_RegistersStartupNotificationSummaryService()
    {
        var services = new ServiceCollection();
        services.AddFluxoPresentation();
        services.AddUIData();
        using var provider = services.BuildServiceProvider();

        var service = provider.GetService<IStartupNotificationSummaryService>();

        Assert.NotNull(service);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~ServiceCollectionExtensionsTests.AddUIData_RegistersStartupNotificationSummaryService" -v minimal`  
Expected: FAIL because `IStartupNotificationSummaryService` is not yet registered.

- [ ] **Step 3: Register new startup summary service in DI**

```csharp
// Fluxo/Extensions/ServiceCollectionExtensions.cs
services.AddTransient<IStartupNotificationSummaryService, StartupNotificationSummaryService>();
```

- [ ] **Step 4: Wire popup orchestration inside App tray startup path**

```csharp
// Fluxo/App.xaml.cs (new fields)
private StartupNotificationPopup? _startupNotificationPopup;
private bool _startupTrayPopupShownThisProcess;

// App.OnStartup, inside _launchInTrayMode branch after HideMainWindowToTray(mainWindow)
await TryShowStartupTrayNotificationPopupAsync();

private async Task TryShowStartupTrayNotificationPopupAsync()
{
    if (!_launchInTrayMode)
        return;

    var summaryService = _serviceProvider?.GetService<IStartupNotificationSummaryService>();
    if (summaryService is null)
        return;

    var policy = new StartupTrayPopupDisplayPolicy();
    var summary = await summaryService.BuildAsync();
    var shouldShow = policy.ShouldShow(_launchInTrayMode, _startupTrayPopupShownThisProcess, summary is not null);
    if (!shouldShow || summary is null)
        return;

    _startupNotificationPopup ??= new StartupNotificationPopup();
    _startupNotificationPopup.OpenAppRequested -= OnStartupPopupOpenRequested;
    _startupNotificationPopup.OpenAppRequested += OnStartupPopupOpenRequested;

    var cursor = Forms.Cursor.Position;
    _startupNotificationPopup.ShowNearScreenPoint(new System.Windows.Point(cursor.X, cursor.Y), summary.Message);
    _startupTrayPopupShownThisProcess = true;
}

private void OnStartupPopupOpenRequested(object? sender, EventArgs e)
{
    RestoreMainWindowFromTray();
}
```

- [ ] **Step 5: Ensure cleanup/disposal includes startup popup**

```csharp
// Fluxo/App.xaml.cs inside DisposeTrayResources()
if (_startupNotificationPopup is not null)
{
    _startupNotificationPopup.Close();
    _startupNotificationPopup = null;
}
```

- [ ] **Step 6: Run DI + startup summary targeted suite**

Run: `dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~ServiceCollectionExtensionsTests|FullyQualifiedName~StartupNotificationSummaryServiceTests|FullyQualifiedName~StartupTrayPopupDisplayPolicyTests" -v minimal`  
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add Fluxo/Extensions/ServiceCollectionExtensions.cs Fluxo/App.xaml.cs Fluxo.Tests/Extensions/ServiceCollectionExtensionsTests.cs
git commit -m "feat: show startup grouped notification popup on tray autostart"
```

## Task 5: Full Verification and Regression Pass

**Files:**
- Modify: `Fluxo.Tests/Services/Notifications/StartupNotificationSummaryServiceTests.cs` (if edge cases need adjustments)
- Modify: `Fluxo.Tests/Views/Shell/Tray/StartupNotificationPopupLayoutTests.cs` (if selector assertions need stabilization)

- [ ] **Step 1: Add edge-case test for multi-item single group and plural goal deadlines**

```csharp
[Fact]
public async Task BuildAsync_SingleGoalDeadlineGroupMultipleItems_UsesPluralGoalMessage()
{
    var sut = CreateSut(
    [
        new Notification
        {
            Id = 1,
            Type = "GoalDeadline-9_20260501",
            Header = "Goal Deadline - Emergency Fund",
            Message = "Emergency Fund ends soon.",
            CreatedOn = DateTime.Today
        },
        new Notification
        {
            Id = 2,
            Type = "GoalDeadline-10_20260501",
            Header = "Goal Deadline - Travel",
            Message = "Travel ends soon.",
            CreatedOn = DateTime.Today.AddMinutes(-1)
        }
    ]);

    var summary = await sut.BuildAsync();

    Assert.NotNull(summary);
    Assert.Equal("There are 2 goals reaching their deadlines", summary!.Message);
}
```

- [ ] **Step 2: Run all startup/tray/notification unit tests**

Run: `dotnet test Fluxo.Tests/Fluxo.Tests.csproj --filter "FullyQualifiedName~Startup|FullyQualifiedName~NotificationGroupingServiceTests|FullyQualifiedName~NotificationPanelVMTests" -v minimal`  
Expected: PASS.

- [ ] **Step 3: Run full test project**

Run: `dotnet test Fluxo.Tests/Fluxo.Tests.csproj -v minimal`  
Expected: PASS.

- [ ] **Step 4: Manual QA for runtime behavior (autostart-only trigger and 5-second dismiss)**

Run (local QA checklist):
1. Start app normally (no `--startup-tray`) -> no startup popup.
2. Start app with `--startup-tray` and one grouped notification -> contextual single-group message appears.
3. Start app with `--startup-tray` and multiple groups -> `There are <groupCount> notifications`.
4. Verify popup auto-closes after 5 seconds.
5. Verify `AngleRight` opens app.
6. Verify `Close` dismisses popup and keeps app in tray.
7. Manually close window to tray later in session -> startup popup does not reappear.

- [ ] **Step 5: Commit**

```bash
git add Fluxo.Tests/Services/Notifications/StartupNotificationSummaryServiceTests.cs Fluxo.Tests/Views/Shell/Tray/StartupNotificationPopupLayoutTests.cs
git commit -m "test: add startup tray grouped notification popup coverage"
```

## Spec Coverage Check

- Autostart-only trigger: covered by Task 3 policy + Task 4 App integration.
- One-time first load from tray: covered by Task 3 + `_startupTrayPopupShownThisProcess` in Task 4.
- Group-based rule selection: covered by Task 1 service logic.
- Single-group custom wording:
  - fixed expense singular uses expense name: Task 1 tests/logic.
  - upcoming payment singular uses card/source name: Task 1 tests/logic.
  - goal deadline singular/plural: Task 1 and Task 5 tests/logic.
  - fallback singular uses header: Task 1 tests/logic.
- Multi-group message uses group count: Task 1 tests/logic.
- Exception/internal issue shows nothing: Task 1 exception test returning null.
- Popup UI with circular `AngleRight` and `Close`: Task 2.
- Close-only behavior for `Close` button: Task 2 event contract + Task 4 wiring.
- Auto-dismiss after 5 seconds: Task 2 timer behavior.

## Placeholder Scan

No `TBD`, `TODO`, or deferred placeholders are present. Every task contains concrete files, commands, and code snippets.

## Type Consistency Check

- Summary service contract: `IStartupNotificationSummaryService.BuildAsync(CancellationToken)`.
- Summary model: `StartupNotificationSummary`.
- Popup class: `StartupNotificationPopup`.
- Display policy class: `StartupTrayPopupDisplayPolicy`.
- Existing grouping type remains `INotificationGroupingService`.
