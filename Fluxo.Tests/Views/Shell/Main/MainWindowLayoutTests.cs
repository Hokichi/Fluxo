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
    public void AnalyticsDrawerTabTrigger_RemainsAvailable()
    {
        var xaml = MainWindowXaml.Value;
        var xamlDocument = MainWindowXamlDocument.Value;

        Assert.Contains("Click=\"OnAnalyticsDrawerTabClick\"", xaml);

        var analyticsDrawerTabButton = xamlDocument
            .Descendants(PresentationNamespace + "Button")
            .SingleOrDefault(button => (string?)button.Attribute(XamlNamespace + "Name") == "AnalyticsDrawerTabButton");

        Assert.NotNull(analyticsDrawerTabButton);
        Assert.Equal("OnAnalyticsDrawerTabClick", (string?)analyticsDrawerTabButton!.Attribute("Click"));
    }

    [Fact]
    public void SpendingAmountGate_HideMarkers_ArePresent()
    {
        var xamlDocument = MainWindowXamlDocument.Value;

        AssertElementHasNameAndStyle(xamlDocument, "Grid", "HeaderSearchRegion", "HideWhenDashboardSpendingAmountGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "customControls:BalloonButton", "HeaderQuickAddButton", "HeaderButtonHideWhenDashboardSpendingAmountGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "Button", "QuickAddMenuButton", "HeaderMenuActionHideWhenDashboardSpendingAmountGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "Button", "SourcesMenuButton", "HeaderMenuActionHideWhenDashboardSpendingAmountGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "Button", "UndoMenuButton", "HeaderMenuActionHideWhenDashboardSpendingAmountGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "Button", "RedoMenuButton", "HeaderMenuActionHideWhenDashboardSpendingAmountGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "Button", "ViewAllSpendingSourcesButton", "TextOnlyButtonHideWhenDashboardSpendingAmountGateLockedStyle");
        AssertElementHasNameAndStyle(xamlDocument, "Button", "AddSpendingSourceButton", "SpendingSourceAddButtonHideWhenDashboardSpendingAmountGateLockedStyle");

        var analyticsTabHost = xamlDocument
            .Descendants(PresentationNamespace + "Border")
            .SingleOrDefault(border => (string?)border.Attribute(XamlNamespace + "Name") == "AnalyticsDrawerTabHost");

        Assert.NotNull(analyticsTabHost);
        Assert.Contains(
            "Binding=\"{Binding IsDashboardSpendingAmountGateLocked}\" Value=\"True\"",
            analyticsTabHost!.ToString(SaveOptions.DisableFormatting));
    }

    [Fact]
    public void SpendingAmountGate_DashboardOverlay_UsesRequiredMessageAndWordmark()
    {
        var xamlDocument = MainWindowXamlDocument.Value;

        var overlay = xamlDocument
            .Descendants(PresentationNamespace + "Grid")
            .SingleOrDefault(grid => (string?)grid.Attribute(XamlNamespace + "Name") == "DashboardSpendingAmountGateOverlay");

        Assert.NotNull(overlay);

        var textBlock = overlay!
            .Descendants(PresentationNamespace + "TextBlock")
            .Single();

        var runs = textBlock
            .Descendants(PresentationNamespace + "Run")
            .ToList();

        Assert.Equal(3, runs.Count);
        Assert.Equal("Add a spending amount to start using ", (string?)runs[0].Attribute("Text"));
        Assert.Equal("flux", (string?)runs[1].Attribute("Text"));
        Assert.Equal("o", (string?)runs[2].Attribute("Text"));
        Assert.Equal("{StaticResource Brush.Mint}", (string?)runs[2].Attribute("Foreground"));
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
        Assert.Contains("BlurEffect Radius=\"8\"", lockStyle!.ToString(SaveOptions.DisableFormatting));
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
}
