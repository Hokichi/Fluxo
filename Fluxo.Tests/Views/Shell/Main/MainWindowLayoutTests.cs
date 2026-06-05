using System.IO;
using System.Linq;
using System.Xml.Linq;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowLayoutTests
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Lazy<string> MainWindowXaml = new(LoadMainWindowXaml);
    private static readonly Lazy<XDocument> MainWindowXamlDocument = new(() => XDocument.Parse(MainWindowXaml.Value));
    private static readonly Lazy<string> DashboardXaml = new(LoadDashboardXaml);
    private static readonly Lazy<XDocument> DashboardXamlDocument = new(() => XDocument.Parse(DashboardXaml.Value));

    [Fact]
    public void HeaderMenu_DoesNotExposeAnalyticsActionButton()
    {
        var xaml = MainWindowXaml.Value;
        var xamlDocument = MainWindowXamlDocument.Value;

        Assert.DoesNotContain("Click=\"OnAnalyticsButtonClick\"", xaml);

        var headerMenuPopup = xamlDocument
            .Descendants(PresentationNamespace + "Popup")
            .SingleOrDefault(popup => (string?)popup.Attribute(XamlNamespace + "Name") == "HeaderMenuPopup");

        Assert.NotNull(headerMenuPopup);

        var analyticsActionEntry = headerMenuPopup!
            .Descendants(PresentationNamespace + "Button")
            .SingleOrDefault(button => (string?)button.Attribute("Click") == "OnAnalyticsButtonClick");

        Assert.Null(analyticsActionEntry);
    }

    [Fact]
    public void FloatingSideNavigation_ExposesFourMainPagesAndNoDrawer()
    {
        var xaml = MainWindowXaml.Value;
        var xamlDocument = MainWindowXamlDocument.Value;

        Assert.DoesNotContain("DrawerTabHost", xaml);
        Assert.DoesNotContain("AnalyticsDrawerLayer", xaml);
        Assert.DoesNotContain("AnalyticsDrawerPanel", xaml);
        Assert.DoesNotContain("AnalyticsDrawerContentHost", xaml);
        Assert.DoesNotContain("OnAnalyticsDrawerTabClick", xaml);
        Assert.DoesNotContain("OnCalendarDrawerTabClick", xaml);
        Assert.DoesNotContain("OnLedgerDrawerTabClick", xaml);

        AssertElementHasNameAndStyle(xamlDocument, "Border", "FloatingSideNavigationRail", "FloatingSideNavigationRailStyle");
        AssertElementHasNameAndStyle(xamlDocument, "ToggleButton", "HomeNavigationButton", "FloatingSideNavigationButtonStyle");
        AssertElementHasNameAndStyle(xamlDocument, "ToggleButton", "AnalyticsNavigationButton", "FloatingSideNavigationButtonStyle");
        AssertElementHasNameAndStyle(xamlDocument, "ToggleButton", "CalendarNavigationButton", "FloatingSideNavigationButtonStyle");
        AssertElementHasNameAndStyle(xamlDocument, "ToggleButton", "LedgerNavigationButton", "FloatingSideNavigationButtonStyle");

        var pageHost = xamlDocument
            .Descendants(PresentationNamespace + "ContentControl")
            .SingleOrDefault(control => (string?)control.Attribute(XamlNamespace + "Name") == "MainPageHost");

        Assert.NotNull(pageHost);
        Assert.Contains("Path=\"{StaticResource Home}\"", xaml);
        Assert.Contains("Path=\"{StaticResource ChartColumn}\"", xaml);
        Assert.Contains("Path=\"{StaticResource Calendar}\"", xaml);
        Assert.Contains("Path=\"{StaticResource BookAiFill}\"", xaml);
    }

    [Fact]
    public void PopupOverlay_BlursContentAndFloatingNavigation()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());

        var applyPopupBlur = ExtractMethodBodyBySignature(source, "private void ApplyPopupBlur()");
        var clearPopupBlur = ExtractMethodBodyBySignature(source, "private void ClearPopupBlur()");

        Assert.Contains("ContentGrid.Effect = CreatePopupBlurEffect();", applyPopupBlur);
        Assert.Contains("FloatingSideNavigationRail.Effect = CreatePopupBlurEffect();", applyPopupBlur);
        Assert.DoesNotContain("AnalyticsDrawerLayer", applyPopupBlur);
        Assert.DoesNotContain("DrawerTabHost", applyPopupBlur);

        Assert.Contains("ContentGrid.Effect = null;", clearPopupBlur);
        Assert.Contains("FloatingSideNavigationRail.Effect = null;", clearPopupBlur);
        Assert.DoesNotContain("AnalyticsDrawerLayer", clearPopupBlur);
        Assert.DoesNotContain("DrawerTabHost", clearPopupBlur);
    }

    [Fact]
    public void StateChange_SetsWindowLayoutStateBeforeFadeInStarts()
    {
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());

        Assert.Contains("public static readonly DependencyProperty IsWindowLayoutMaximizedProperty", source);

        var animateToMaximized = ExtractMethodBodyBySignature(source, "private void AnimateToMaximized()");
        var animateToRestored = ExtractMethodBodyBySignature(source, "private void AnimateToRestored()");
        var animateStateChange = ExtractMethodBodyBySignature(source, "private void AnimateStateChange(Rect from, Rect to, bool maximizing)");

        Assert.DoesNotContain("IsWindowLayoutMaximized = true;", animateToMaximized);
        Assert.DoesNotContain("IsWindowLayoutMaximized = false;", animateToRestored);
        Assert.Contains("FadeContentIn(() =>", animateStateChange);
        Assert.Contains("IsWindowLayoutMaximized = maximizing;", animateStateChange);
        Assert.True(
            animateStateChange.IndexOf("IsWindowLayoutMaximized = maximizing;", StringComparison.Ordinal) <
            animateStateChange.IndexOf("FadeContentIn(() =>", StringComparison.Ordinal));
    }

    [Fact]
    public void SpendingAmountGate_HideMarkers_ArePresent()
    {
        var xamlDocument = MainWindowXamlDocument.Value;

        AssertElementHasNameAndStyle(xamlDocument, "Grid", "HeaderSearchRegion", "HideWhenSufficientFundsActionGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "customControls:BalloonButton", "HeaderQuickAddButton", "HeaderButtonHideWhenSufficientFundsActionGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "Button", "QuickAddMenuButton", "HeaderMenuActionHideWhenSufficientFundsActionGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "Button", "UndoMenuButton", "HeaderMenuActionHideWhenSufficientFundsActionGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "Button", "RedoMenuButton", "HeaderMenuActionHideWhenSufficientFundsActionGateLockedStyle");
    }

    [Fact]
    public void SpendingAmountGate_HeaderMenuSourcesRemainAvailable()
    {
        var xamlDocument = MainWindowXamlDocument.Value;

        AssertElementHasNameAndStyle(xamlDocument, "Button", "SourcesMenuButton", "HeaderMenuActionButtonStyle");
    }

    [Fact]
    public void MainWindow_KeepsDashboardContentInShellWithCorrectControlOwnership()
    {
        var xamlDocument = MainWindowXamlDocument.Value;
        var dashboardXaml = DashboardXaml.Value;
        var dashboardXamlDocument = DashboardXamlDocument.Value;

        AssertElementHasNameAndStyle(xamlDocument, "Grid", "DashboardSpendingAmountGateContent", "DashboardSpendingAmountGateLockedContentStyle");
        AssertElementHasName(xamlDocument, "DaySpinnerControl", "DaySpinnerControlHost");
        AssertElementHasName(xamlDocument, "ContentControl", "MainPageHost");
        AssertElementHasName(xamlDocument, "Grid", "DashboardSpendingAmountGateOverlay");
        Assert.DoesNotContain("DashboardPageHost", MainWindowXaml.Value);
        Assert.DoesNotContain("OutgoingMainPageHost", MainWindowXaml.Value);

        Assert.DoesNotContain("DaySpinnerControlHost", dashboardXaml);
        AssertElementHasName(dashboardXamlDocument, "MainViewModeToggleControl", "ViewModeToggleControlHost");
        AssertElementHasName(dashboardXamlDocument, "Grid", "MainContentGrid");
        AssertElementHasName(dashboardXamlDocument, "Button", "ViewAllSpendingSourcesButton");
        AssertElementHasName(dashboardXamlDocument, "Button", "AddSpendingSourceButton");
        AssertElementHasName(dashboardXamlDocument, "SpentAllowancePanel", "SpentAllowancePanelHost");
        AssertElementHasName(dashboardXamlDocument, "BudgetAllocationPanel", "BudgetAllocationPanelHost");
        AssertElementHasName(dashboardXamlDocument, "SavingGoalsPanel", "SavingGoalsPanelHost");
        AssertElementHasName(dashboardXamlDocument, "NotificationPanel", "NotificationPanelHost");
        AssertElementHasName(dashboardXamlDocument, "FadingScrollViewer", "DashboardSpendingSourcesScrollViewer");
        AssertElementHasName(dashboardXamlDocument, "Button", "DashboardSpendingSourcesScrollLeftButton");
        AssertElementHasName(dashboardXamlDocument, "Button", "DashboardSpendingSourcesScrollRightButton");
    }

    [Fact]
    public void DashboardSpendingSources_HasOverflowScrollButtonsInMainWindow()
    {
        var xaml = DashboardXaml.Value;
        var source = File.ReadAllText(ResolveDashboardCodeBehindPath());
        var icons = File.ReadAllText(RepositoryPaths.File(
            "Fluxo.Resources",
            "Resources",
            "Icons.xaml"));

        Assert.Contains("x:Key=\"AngleLeft\"", icons);
        Assert.Contains("x:Key=\"AngleRight\"", icons);
        Assert.Contains("x:Key=\"DashboardSpendingSourcesScrollButtonStyle\"", xaml);
        Assert.Contains("DashboardSpendingSourcesScrollPixels = 10", source);
        Assert.Contains("DashboardSpendingSourcesScrollIntervalMilliseconds = 10", source);
        Assert.Contains("_dashboardSpendingSourcesScrollTimer.Tick += OnDashboardSpendingSourcesScrollTimerTick;", source);
        Assert.Contains("private void OnDashboardSpendingSourcesScrollTimerTick(object? sender, EventArgs e)", source);
        Assert.Contains("ScrollDashboardSpendingSources(", source);
        Assert.Contains("DashboardSpendingSourcesScrollViewer.ScrollableWidth > 0", source);
    }

    private static void AssertElementHasName(XDocument xamlDocument, string elementName, string xName)
    {
        var localName = elementName.Contains(':')
            ? elementName[(elementName.IndexOf(':') + 1)..]
            : elementName;

        var element = xamlDocument
            .Descendants()
            .SingleOrDefault(node =>
                node.Name.LocalName == localName &&
                (string?)node.Attribute(XamlNamespace + "Name") == xName);

        Assert.NotNull(element);
    }

    private static void AssertElementHasNameAndStyle(XDocument xamlDocument, string elementName, string xName, string styleKey)
    {
        var localName = elementName.Contains(':')
            ? elementName[(elementName.IndexOf(':') + 1)..]
            : elementName;

        var element = xamlDocument
            .Descendants()
            .SingleOrDefault(node =>
                node.Name.LocalName == localName &&
                (string?)node.Attribute(XamlNamespace + "Name") == xName);

        Assert.NotNull(element);
        Assert.Equal($"{{StaticResource {styleKey}}}", (string?)element!.Attribute("Style"));
    }

    private static string LoadMainWindowXaml()
    {
        var mainWindowXamlPath = ResolveMainWindowXamlPath();
        return File.ReadAllText(mainWindowXamlPath);
    }

    private static string LoadDashboardXaml()
    {
        var dashboardXamlPath = ResolveDashboardXamlPath();
        return File.ReadAllText(dashboardXamlPath);
    }

    private static string ResolveMainWindowXamlPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
            {
                var mainWindowXamlPath = Path.Combine(
                    currentDirectory.FullName,
                    "Fluxo",
                    "Views",
                    "Shell",
                    "Main",
                    "MainWindow.xaml");

                if (!File.Exists(mainWindowXamlPath))
                {
                    throw new FileNotFoundException($"MainWindow.xaml was not found at '{mainWindowXamlPath}'.", mainWindowXamlPath);
                }

                return mainWindowXamlPath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }

    private static string ResolveMainWindowCodeBehindPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
            {
                var mainWindowCodeBehindPath = Path.Combine(
                    currentDirectory.FullName,
                    "Fluxo",
                    "Views",
                    "Shell",
                    "Main",
                    "MainWindow.xaml.cs");

                if (!File.Exists(mainWindowCodeBehindPath))
                {
                    throw new FileNotFoundException(
                        $"MainWindow.xaml.cs was not found at '{mainWindowCodeBehindPath}'.",
                        mainWindowCodeBehindPath);
                }

                return mainWindowCodeBehindPath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }

    private static string ResolveDashboardXamlPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
            {
                var dashboardXamlPath = Path.Combine(
                    currentDirectory.FullName,
                    "Fluxo",
                    "Views",
                    "Shell",
                    "Main",
                    "Pages",
                    "Dashboard.xaml");

                if (!File.Exists(dashboardXamlPath))
                {
                    throw new FileNotFoundException($"Dashboard.xaml was not found at '{dashboardXamlPath}'.", dashboardXamlPath);
                }

                return dashboardXamlPath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }

    private static string ResolveDashboardCodeBehindPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
            {
                var dashboardCodeBehindPath = Path.Combine(
                    currentDirectory.FullName,
                    "Fluxo",
                    "Views",
                    "Shell",
                    "Main",
                    "Pages",
                    "Dashboard.xaml.cs");

                if (!File.Exists(dashboardCodeBehindPath))
                {
                    throw new FileNotFoundException(
                        $"Dashboard.xaml.cs was not found at '{dashboardCodeBehindPath}'.",
                        dashboardCodeBehindPath);
                }

                return dashboardCodeBehindPath;
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }

    private static string ExtractMethodBodyBySignature(string source, string signatureMarker)
    {
        var signatureIndex = source.IndexOf(signatureMarker, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature '{signatureMarker}' was not found in MainWindow.xaml.cs.");

        var openingBraceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(openingBraceIndex >= 0, $"Opening brace for method signature '{signatureMarker}' was not found.");

        var depth = 0;
        for (var index = openingBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
                continue;
            }

            if (source[index] != '}')
                continue;

            depth--;
            if (depth != 0)
                continue;

            return source.Substring(openingBraceIndex + 1, index - openingBraceIndex - 1);
        }

        throw new InvalidOperationException($"Closing brace for method signature '{signatureMarker}' was not found.");
    }
}
