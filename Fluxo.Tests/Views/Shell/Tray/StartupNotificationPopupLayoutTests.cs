using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Tray;

public sealed class StartupNotificationPopupLayoutTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string PopupXamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Fluxo",
        "Fluxo",
        "Views",
        "Shell",
        "Tray",
        "StartupNotificationPopup.xaml"));

    private static readonly string PopupCodeBehindPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Fluxo",
        "Fluxo",
        "Views",
        "Shell",
        "Tray",
        "StartupNotificationPopup.xaml.cs"));

    private static XDocument LoadPopupXaml() => XDocument.Parse(File.ReadAllText(PopupXamlPath));

    [Fact]
    public void PopupUsesAngleRightGeometry_ForOpenAppAction()
    {
        var popupXaml = File.ReadAllText(PopupXamlPath);

        Assert.Contains("Data=\"{StaticResource AngleRight}\"", popupXaml);
    }

    [Fact]
    public void PopupIncludesCloseButtonText_ForDismissAction()
    {
        var document = LoadPopupXaml();
        var closeTextBlock = document
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "TextBlock" &&
                (string?)element.Attribute("Text") == "Close");

        Assert.NotNull(closeTextBlock);
    }

    [Fact]
    public void PopupButtonsUseWizardCircleActionButtonStyle()
    {
        var document = LoadPopupXaml();
        var buttons = document
            .Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .ToList();

        Assert.Equal(2, buttons.Count);
        Assert.All(buttons, button =>
            Assert.Equal("{StaticResource WizardCircleActionButtonStyle}", (string?)button.Attribute("Style")));
    }

    [Fact]
    public void PopupSummaryTextConstrainsWidthForWrapping()
    {
        var document = LoadPopupXaml();
        var summaryTextBlock = Assert.Single(document
            .Descendants()
            .Where(element => element.Name.LocalName == "TextBlock" &&
                              (string?)element.Attribute(XamlNamespace + "Name") == "SummaryTextBlock"));

        Assert.Equal("260", (string?)summaryTextBlock.Attribute("MaxWidth"));
        Assert.Equal("WrapWithOverflow", (string?)summaryTextBlock.Attribute("TextWrapping"));
    }

    [Fact]
    public void PopupCodeBehindUsesSafeClampHelper_ForPlacementBounds()
    {
        var codeBehind = File.ReadAllText(PopupCodeBehindPath);

        Assert.Contains("private static double SafeClamp", codeBehind);
        Assert.Contains("if (max < min)", codeBehind);
        Assert.Contains("Left = SafeClamp(", codeBehind);
        Assert.Contains("Top = SafeClamp(", codeBehind);
    }

    [Fact]
    public void PopupCodeBehindDetachesLifecycleHandlers_OnClosed()
    {
        var codeBehind = File.ReadAllText(PopupCodeBehindPath);

        Assert.Contains("_autoCloseTimer.Tick -= OnAutoCloseTimerTick;", codeBehind);
        Assert.Contains("Deactivated -= OnDeactivated;", codeBehind);
        Assert.Contains("MouseEnter -= OnMouseEnter;", codeBehind);
        Assert.Contains("MouseLeave -= OnMouseLeave;", codeBehind);
        Assert.Contains("Closed -= OnClosed;", codeBehind);
    }
}
