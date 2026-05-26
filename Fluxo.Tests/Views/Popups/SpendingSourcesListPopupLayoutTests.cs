using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class SpendingSourcesListPopupLayoutTests
{
    private static readonly string PopupXamlPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Popups",
        "SpendingSourcesListPopup.xaml");

    private static readonly string PopupCodeBehindPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Popups",
        "SpendingSourcesListPopup.xaml.cs");

    [Fact]
    public void AddNewSourceButton_IsAboveScrollingSourceList()
    {
        var xaml = File.ReadAllText(PopupXamlPath);

        Assert.Contains("x:Name=\"AddNewSourceButton\"", xaml);
        Assert.Contains("Content=\"+ Add New Source\"", xaml);
        Assert.Contains("Click=\"OnAddNewSourceClick\"", xaml);
        Assert.True(
            xaml.IndexOf("x:Name=\"AddNewSourceButton\"", StringComparison.Ordinal) <
            xaml.IndexOf("<customControls:FadingScrollViewer", StringComparison.Ordinal),
            "The add-source button should be outside and above the scrolling list.");
    }

    [Fact]
    public void AddNewSourceButton_HandsOffToAddSpendingSourcePopup()
    {
        var source = File.ReadAllText(PopupCodeBehindPath);

        Assert.Contains("private void OnAddNewSourceClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("CloseForPopupHandoff();", source);
        Assert.Contains("ownerWindow.OpenAddSpendingSourcePopup()", source);
    }
}
