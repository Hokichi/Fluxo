using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AccountsListPopupLayoutTests
{
    private static readonly string PopupXamlPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Popups",
        "AccountsListPopup.xaml");

    private static readonly string PopupCodeBehindPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Popups",
        "AccountsListPopup.xaml.cs");

    [Fact]
    public void AddNewAccountButton_IsBelowScrollingAccountList()
    {
        var xaml = File.ReadAllText(PopupXamlPath);

        Assert.Contains("x:Name=\"AddNewSourceButton\"", xaml);
        Assert.Contains("Grid.Row=\"2\"", xaml);
        Assert.Contains("ButtonText=\"Add New Account\"", xaml);
        Assert.Contains("Click=\"OnAddNewSourceClick\"", xaml);
        Assert.Contains("<customControls:FadingScrollViewer", xaml);
    }

    [Fact]
    public void AddNewSourceButton_HandsOffToAddAccountPopup()
    {
        var source = File.ReadAllText(PopupCodeBehindPath);

        Assert.Contains("private void OnAddNewSourceClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("CloseForPopupHandoff();", source);
        Assert.Contains("ownerWindow.OpenAddAccountPopup()", source);
    }
}
