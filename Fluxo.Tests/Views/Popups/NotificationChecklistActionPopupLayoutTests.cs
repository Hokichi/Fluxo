using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class NotificationChecklistActionPopupLayoutTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string PopupXamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Fluxo",
        "Fluxo",
        "Views",
        "Popups",
        "NotificationChecklistActionPopup.xaml"));

    private static XDocument LoadPopupXaml() => XDocument.Parse(File.ReadAllText(PopupXamlPath));

    private static XElement GetChecklistItemTemplate(XDocument document)
    {
        var template = document
            .Descendants()
            .FirstOrDefault(element =>
                element.Name.LocalName == "DataTemplate" &&
                (string?)element.Attribute(XamlNamespace + "Key") == "NotificationChecklistItemTemplate");

        return Assert.IsType<XElement>(template);
    }

    [Fact]
    public void PopupUsesSegmentedActionOptions_ForEachNotificationItem()
    {
        var document = LoadPopupXaml();
        var template = GetChecklistItemTemplate(document);
        var radioButtons = template.Descendants().Where(element => element.Name.LocalName == "RadioButton").ToList();

        Assert.Equal(3, radioButtons.Count);

        var ignoreOption = Assert.Single(radioButtons.Where(element => (string?)element.Attribute("Content") == "Ignore"));
        Assert.Equal("{Binding IsIgnoreSelected, Mode=TwoWay}", (string?)ignoreOption.Attribute("IsChecked"));

        var paidOption = Assert.Single(radioButtons.Where(element => (string?)element.Attribute("Content") == "Paid"));
        Assert.Equal("{Binding IsPaidSelected, Mode=TwoWay}", (string?)paidOption.Attribute("IsChecked"));

        var processOption = Assert.Single(radioButtons.Where(element => (string?)element.Attribute("Content") == "Process"));
        Assert.Equal("{Binding IsProcessSelected, Mode=TwoWay}", (string?)processOption.Attribute("IsChecked"));

        Assert.All(radioButtons, element =>
        {
            Assert.Equal("{StaticResource SegmentedToggleOptionStyle}", (string?)element.Attribute("Style"));
            Assert.Null(element.Attribute("GroupName"));
        });
    }

    [Fact]
    public void PopupNoLongerUsesCheckboxRowStyle()
    {
        var document = LoadPopupXaml();
        var template = GetChecklistItemTemplate(document);
        var checkBoxes = template.Descendants().Where(element => element.Name.LocalName == "CheckBox");
        var styles = template.Descendants().Attributes("Style").Select(attribute => attribute.Value);

        Assert.Empty(checkBoxes);
        Assert.DoesNotContain("{StaticResource ItemCheckBoxWithContentStyle}", styles);
    }

    [Fact]
    public void ProcessActionSourceSelector_UsesExpectedBindings()
    {
        var document = LoadPopupXaml();
        var template = GetChecklistItemTemplate(document);
        var comboBox = Assert.Single(template.Descendants().Where(element => element.Name.LocalName == "ComboBox"));

        Assert.Equal("{Binding AvailableSources}", (string?)comboBox.Attribute("ItemsSource"));
        Assert.Equal("{Binding SelectedSourceId, Mode=TwoWay}", (string?)comboBox.Attribute("SelectedValue"));
        Assert.Equal("Id", (string?)comboBox.Attribute("SelectedValuePath"));
        Assert.Equal("Name", (string?)comboBox.Attribute("DisplayMemberPath"));
        Assert.Equal("{Binding ShowSourceSelector, Converter={StaticResource BoolToVisibilityConverter}}", (string?)comboBox.Attribute("Visibility"));
    }

    [Fact]
    public void PopupUsesActionOrientedTitleAndInstructionCopy()
    {
        var document = LoadPopupXaml();
        var root = Assert.IsType<XElement>(document.Root);
        var textBlocks = document.Descendants().Where(element => element.Name.LocalName == "TextBlock").ToList();

        Assert.Equal("Review Notification Actions", (string?)root.Attribute("PopupTitle"));
        Assert.Contains(textBlocks, element => (string?)element.Attribute("Text") == "Choose Ignore, Paid, or Process for each notification");
    }
}
