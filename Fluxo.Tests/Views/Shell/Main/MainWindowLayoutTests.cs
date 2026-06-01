using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowLayoutTests
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly Lazy<string> MainWindowXaml = new(LoadMainWindowXaml);
    private static readonly Lazy<XDocument> MainWindowXamlDocument = new(() => XDocument.Parse(MainWindowXaml.Value));

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
    public void DrawerTabTrigger_ExposesAnalyticsAndCalendarTabs()
    {
        var xaml = MainWindowXaml.Value;
        var xamlDocument = MainWindowXamlDocument.Value;

        Assert.Contains("Click=\"OnAnalyticsDrawerTabClick\"", xaml);
        Assert.Contains("Click=\"OnCalendarDrawerTabClick\"", xaml);

        var analyticsDrawerTabButton = xamlDocument
            .Descendants(PresentationNamespace + "Button")
            .SingleOrDefault(button => (string?)button.Attribute(XamlNamespace + "Name") == "AnalyticsDrawerTabButton");
        var calendarDrawerTabButton = xamlDocument
            .Descendants(PresentationNamespace + "Button")
            .SingleOrDefault(button => (string?)button.Attribute(XamlNamespace + "Name") == "CalendarDrawerTabButton");

        Assert.NotNull(analyticsDrawerTabButton);
        Assert.NotNull(calendarDrawerTabButton);
        Assert.Equal("OnAnalyticsDrawerTabClick", (string?)analyticsDrawerTabButton!.Attribute("Click"));
        Assert.Equal("OnCalendarDrawerTabClick", (string?)calendarDrawerTabButton!.Attribute("Click"));
    }

    [Fact]
    public void DrawerHeader_DateRangeSelectorHost_IsNamedAndControlledForAnalyticsOnly()
    {
        var xamlDocument = MainWindowXamlDocument.Value;
        var source = File.ReadAllText(ResolveMainWindowCodeBehindPath());

        var dateRangeSelectorHost = xamlDocument
            .Descendants(PresentationNamespace + "Border")
            .SingleOrDefault(border => (string?)border.Attribute(XamlNamespace + "Name") == "AnalyticsDateRangeSelectorHost");

        Assert.NotNull(dateRangeSelectorHost);
        var dateSelectors = dateRangeSelectorHost!
            .Descendants()
            .Where(element => element.Name.LocalName == "DateSelector")
            .ToList();

        Assert.Equal(2, dateSelectors.Count);
        Assert.Equal("{x:Null}", (string?)dateRangeSelectorHost.Attribute("DataContext"));
        Assert.Equal("{Binding StartDate, Mode=TwoWay}", (string?)dateSelectors[0].Attribute("SelectedDate"));
        Assert.Equal("{Binding EndDate, Mode=TwoWay}", (string?)dateSelectors[1].Attribute("SelectedDate"));
        Assert.DoesNotContain("AnalyticsDrawerContentHost", dateRangeSelectorHost.ToString(SaveOptions.DisableFormatting));

        Assert.Contains("SetAnalyticsDateRangeSelectorVisibility(page);", source);
        Assert.Contains("private void SetAnalyticsDateRangeSelectorVisibility(MainDrawerPage page)", source);
        Assert.Contains("AnalyticsDateRangeSelectorHost.Visibility = page switch", source);
        Assert.Contains("MainDrawerPage.Analytics => Visibility.Visible", source);
        Assert.Contains("MainDrawerPage.Calendar => Visibility.Collapsed", source);

        var ensureAnalyticsDrawerLoaded = ExtractMethodBodyBySignature(
            source,
            "private void EnsureAnalyticsDrawerLoaded()");
        var ensureCalendarDrawerLoaded = ExtractMethodBodyBySignature(
            source,
            "private void EnsureCalendarDrawerLoaded()");

        Assert.Contains(
            "AnalyticsDateRangeSelectorHost.DataContext = _analyticsDrawerView.DataContext;",
            ensureAnalyticsDrawerLoaded);
        Assert.DoesNotContain("AnalyticsDateRangeSelectorHost.DataContext", ensureCalendarDrawerLoaded);
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
        AssertElementHasNameAndStyle(xamlDocument, "Button", "ViewAllSpendingSourcesButton", "TextOnlyButtonStyle");
        AssertElementHasNameAndStyle(xamlDocument, "Button", "AddSpendingSourceButton", "SpendingSourceAddButtonStyle");

        var drawerTabHost = xamlDocument
            .Descendants(PresentationNamespace + "Border")
            .SingleOrDefault(border => (string?)border.Attribute(XamlNamespace + "Name") == "DrawerTabHost");

        Assert.NotNull(drawerTabHost);
        Assert.Contains(
            "Binding=\"{Binding IsSufficientFundsActionGateLocked}\" Value=\"True\"",
            drawerTabHost!.ToString(SaveOptions.DisableFormatting));
    }

    [Fact]
    public void SpendingAmountGate_HeaderMenuSourcesRemainAvailable()
    {
        var xamlDocument = MainWindowXamlDocument.Value;

        AssertElementHasNameAndStyle(xamlDocument, "Button", "SourcesMenuButton", "HeaderMenuActionButtonStyle");
    }

    [Fact]
    public void SpendingAmountGate_DateCarouselAndViewModeToggle_AreInsideLockedContent()
    {
        var xamlDocument = MainWindowXamlDocument.Value;

        var gatedContent = xamlDocument
            .Descendants(PresentationNamespace + "Grid")
            .SingleOrDefault(grid => (string?)grid.Attribute(XamlNamespace + "Name") == "DashboardSpendingAmountGateContent");

        Assert.NotNull(gatedContent);
        Assert.Contains(gatedContent!.Descendants(), element =>
            (string?)element.Attribute(XamlNamespace + "Name") == "DaySpinnerControlHost");
        Assert.Contains(gatedContent.Descendants(), element =>
            (string?)element.Attribute(XamlNamespace + "Name") == "ViewModeToggleControlHost");
    }

    [Fact]
    public void SpendingAmountGate_DashboardOverlay_UsesTextButtonWithRequiredMessageAndWordmark()
    {
        var xamlDocument = MainWindowXamlDocument.Value;

        var overlay = xamlDocument
            .Descendants(PresentationNamespace + "Grid")
            .SingleOrDefault(grid => (string?)grid.Attribute(XamlNamespace + "Name") == "DashboardSpendingAmountGateOverlay");

        Assert.NotNull(overlay);

        var button = overlay!
            .Descendants(PresentationNamespace + "Button")
            .Single();

        Assert.Equal("DashboardSpendingAmountGateActionButton", (string?)button.Attribute(XamlNamespace + "Name"));
        Assert.Equal("OnDashboardSpendingAmountGateActionClick", (string?)button.Attribute("Click"));
        Assert.Equal("{StaticResource TextOnlyButtonStyle}", (string?)button.Attribute("Style"));

        var textBlocks = button
            .Descendants(PresentationNamespace + "TextBlock")
            .ToList();

        Assert.True(textBlocks.Count >= 3);
        Assert.Equal("Insufficient fund. Please include a spending source with sufficient funds to start using ", (string?)textBlocks[0].Attribute("Text"));
        Assert.Equal("flux", (string?)textBlocks[1].Attribute("Text"));
        Assert.Equal("o", (string?)textBlocks[2].Attribute("Text"));
        Assert.Equal("{StaticResource Brush.Mint}", (string?)textBlocks[2].Attribute("Foreground"));
    }

    [Fact]
    public void SpendingAmountGate_DashboardContent_UsesBlurAndHitTestLockStyle()
    {
        var xamlDocument = MainWindowXamlDocument.Value;

        var gatedContent = xamlDocument
            .Descendants(PresentationNamespace + "Grid")
            .SingleOrDefault(grid => (string?)grid.Attribute(XamlNamespace + "Name") == "DashboardSpendingAmountGateContent");

        Assert.NotNull(gatedContent);
        Assert.Equal("{StaticResource DashboardSpendingAmountGateLockedContentStyle}", (string?)gatedContent!.Attribute("Style"));

        var lockStyle = xamlDocument
            .Descendants(PresentationNamespace + "Style")
            .SingleOrDefault(style => (string?)style.Attribute(XamlNamespace + "Key") == "DashboardSpendingAmountGateLockedContentStyle");

        Assert.NotNull(lockStyle);
        Assert.Contains("BlurEffect Radius=\"20\"", lockStyle!.ToString(SaveOptions.DisableFormatting));
        Assert.Contains("Opacity\" Value=\"0.55", lockStyle.ToString(SaveOptions.DisableFormatting));
        Assert.Contains("IsHitTestVisible\" Value=\"False", lockStyle.ToString(SaveOptions.DisableFormatting));
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
