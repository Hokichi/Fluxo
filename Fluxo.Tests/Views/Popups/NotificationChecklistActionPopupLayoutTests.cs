using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class NotificationChecklistActionPopupLayoutTests
{
    private static readonly string PopupXamlPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "Fluxo",
        "Fluxo",
        "Views",
        "Popups",
        "NotificationChecklistActionPopup.xaml"));

    [Fact]
    public void PopupUsesSegmentedActionOptions_ForEachNotificationItem()
    {
        var xaml = File.ReadAllText(PopupXamlPath);

        Assert.Contains("Content=\"Ignore\"", xaml);
        Assert.Contains("Content=\"Paid\"", xaml);
        Assert.Contains("Content=\"Process\"", xaml);
        Assert.Contains("Style=\"{StaticResource SegmentedToggleOptionStyle}\"", xaml);
    }

    [Fact]
    public void PopupNoLongerUsesCheckboxRowStyle()
    {
        var xaml = File.ReadAllText(PopupXamlPath);

        Assert.DoesNotContain("ItemCheckBoxWithContentStyle", xaml);
    }

    [Fact]
    public void ProcessActionSourceSelector_UsesExpectedBindings()
    {
        var xaml = File.ReadAllText(PopupXamlPath);

        Assert.Contains("ItemsSource=\"{Binding AvailableSources}\"", xaml);
        Assert.Contains("SelectedValue=\"{Binding SelectedSourceId, Mode=TwoWay}\"", xaml);
        Assert.Contains("SelectedValuePath=\"Id\"", xaml);
        Assert.Contains("DisplayMemberPath=\"Name\"", xaml);
        Assert.Contains("Visibility=\"{Binding ShowSourceSelector, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
    }
}
