using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Tray;

public sealed class StartupNotificationPopupLayoutTests
{
    private static readonly string PopupXamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Fluxo",
        "Fluxo",
        "Views",
        "Shell",
        "Tray",
        "StartupNotificationPopup.xaml"));

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
}
