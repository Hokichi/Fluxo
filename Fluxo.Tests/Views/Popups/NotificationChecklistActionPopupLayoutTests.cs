using System.IO;
using System.Linq;
using System.Xml.Linq;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class NotificationChecklistActionPopupLayoutTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly string PopupXamlPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Popups",
        "NotificationChecklistActionPopup.xaml");

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
        var groups = template.Descendants().Where(element => element.Name.LocalName == "SegmentedToggleGroup").ToList();
        var options = template.Descendants().Where(element => element.Name.LocalName == "SegmentedToggleOption").ToList();

        Assert.Single(groups);
        Assert.Equal(3, options.Count);

        var ignoreOption = Assert.Single(options.Where(element => (string?)element.Attribute("Content") == "Ignore"));
        Assert.Equal("{Binding IsIgnoreSelected, Mode=TwoWay}", (string?)ignoreOption.Attribute("IsSelected"));

        var paidOption = Assert.Single(options.Where(element => (string?)element.Attribute("Content") == "Paid"));
        Assert.Equal("{Binding IsPaidSelected, Mode=TwoWay}", (string?)paidOption.Attribute("IsSelected"));

        var processOption = Assert.Single(options.Where(element => (string?)element.Attribute("Content") == "Process"));
        Assert.Equal("{Binding IsProcessSelected, Mode=TwoWay}", (string?)processOption.Attribute("IsSelected"));
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
    public void RecurringActionFields_UseExpectedBindings()
    {
        var document = LoadPopupXaml();
        var template = GetChecklistItemTemplate(document);
        var recurringGrid = Assert.Single(template.Descendants().Where(element =>
            element.Name.LocalName == "Grid" &&
            (string?)element.Attribute("Visibility") == "{Binding ShowRecurringFields, Converter={StaticResource BoolToVisibilityConverter}}"));

        Assert.Equal(
            "{Binding ShowRecurringFields, Converter={StaticResource BoolToVisibilityConverter}}",
            (string?)recurringGrid.Attribute("Visibility"));

        var comboBoxes = recurringGrid.Descendants().Where(element => element.Name.LocalName == "ComboBox").ToList();
        Assert.Equal(3, comboBoxes.Count);

        var sourceCombo = Assert.Single(comboBoxes.Where(element => (string?)element.Attribute("ItemsSource") == "{Binding AvailableSources}"));
        Assert.Equal("{Binding SelectedSourceId, Mode=TwoWay}", (string?)sourceCombo.Attribute("SelectedValue"));
        Assert.Equal("Id", (string?)sourceCombo.Attribute("SelectedValuePath"));
        Assert.Equal("Name", (string?)sourceCombo.Attribute("DisplayMemberPath"));

        var tagCombo = Assert.Single(comboBoxes.Where(element => (string?)element.Attribute("ItemsSource") == "{Binding AvailableTags}"));
        Assert.Equal("{Binding SelectedTagId, Mode=TwoWay}", (string?)tagCombo.Attribute("SelectedValue"));
        Assert.Equal("{StaticResource NotificationChecklistTagItemTemplate}", (string?)tagCombo.Attribute("ItemTemplate"));
        Assert.Equal("{StaticResource NotificationChecklistTagsComboBoxStyle}", (string?)tagCombo.Attribute("Style"));
        Assert.Equal("{Binding IsRecurringExpense, Converter={StaticResource BoolToVisibilityConverter}}", (string?)tagCombo.Attribute("Visibility"));

        var goalCombo = Assert.Single(comboBoxes.Where(element => (string?)element.Attribute("ItemsSource") == "{Binding AvailableGoals}"));
        Assert.Equal("{Binding SelectedGoalId, Mode=TwoWay}", (string?)goalCombo.Attribute("SelectedValue"));
        Assert.Equal("{Binding IsRecurringGoalUpdate, Converter={StaticResource BoolToVisibilityConverter}}", (string?)goalCombo.Attribute("Visibility"));
    }

    [Fact]
    public void TagsComboBox_RendersColorDotBeforeTagName()
    {
        var document = LoadPopupXaml();
        var tagTemplate = Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "DataTemplate" &&
            (string?)element.Attribute(XamlNamespace + "Key") == "NotificationChecklistTagItemTemplate"));

        var ellipse = Assert.Single(tagTemplate.Descendants().Where(element => element.Name.LocalName == "Ellipse"));
        Assert.Equal("{Binding HexCode}", (string?)ellipse.Attribute("Fill"));
        Assert.Equal("10", (string?)ellipse.Attribute("Width"));
        Assert.Equal("10", (string?)ellipse.Attribute("Height"));

        var nameText = Assert.Single(tagTemplate.Descendants().Where(element =>
            element.Name.LocalName == "TextBlock" &&
            (string?)element.Attribute("Text") == "{Binding Name}"));
        Assert.Equal("8,0,0,0", (string?)nameText.Attribute("Margin"));
    }

    [Fact]
    public void TagsComboBox_DropDownHasStaticAddTagButtonAboveScrollableItems()
    {
        var document = LoadPopupXaml();
        var tagsComboStyle = Assert.Single(document.Descendants().Where(element =>
            element.Name.LocalName == "Style" &&
            (string?)element.Attribute(XamlNamespace + "Key") == "NotificationChecklistTagsComboBoxStyle"));

        var addButton = Assert.Single(tagsComboStyle.Descendants().Where(element =>
            element.Name.LocalName == "Button" &&
            (string?)element.Attribute("Content") == "+ Add Tag"));
        Assert.Equal("OnAddTagClick", (string?)addButton.Attribute("Click"));

        var addButtonRow = int.Parse((string?)addButton.Attribute("Grid.Row") ?? "0");
        var scrollViewer = Assert.Single(tagsComboStyle.Descendants().Where(element =>
            element.Name.LocalName == "FadingScrollViewer" &&
            element.Descendants().Any(descendant => descendant.Name.LocalName == "ItemsPresenter")));
        var scrollViewerRow = int.Parse((string?)scrollViewer.Attribute("Grid.Row") ?? "0");

        Assert.True(addButtonRow < scrollViewerRow);
    }

    [Fact]
    public void CodeBehind_ProceedShowsProcessingToastThenProcessedDelay()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Popups",
            "NotificationChecklistActionPopup.xaml.cs"));

        Assert.Contains("ToastPopup", source);
        Assert.Contains("\"Processing...\"", source);
        Assert.Contains("\"Processed\"", source);
        Assert.Contains("TimeSpan.FromSeconds(2)", source);
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
