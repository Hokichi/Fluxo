using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class QuickAddPopupLayoutTests
{
    private static readonly string PopupXamlPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Popups",
        "QuickAddPopup.xaml");

    private static readonly string PopupCodeBehindPath = RepositoryPaths.File(
        "Fluxo",
        "Views",
        "Popups",
        "QuickAddPopup.xaml.cs");

    [Fact]
    public void LockedDashboard_DisablesTransactionAndSavingGoalTilesOnly()
    {
        var xaml = File.ReadAllText(PopupXamlPath);

        Assert.Contains("x:Key=\"LockedDisabledQuickAddTileButtonStyle\"", xaml);
        Assert.Contains("x:Name=\"NewSpendingSourceQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"NewTransactionQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"NewSavingGoalQuickAddButton\"", xaml);
        Assert.Equal(2, xaml.Split("Style=\"{StaticResource LockedDisabledQuickAddTileButtonStyle}\"").Length - 1);
        Assert.Contains("Style=\"{StaticResource QuickAddTileButtonStyle}\"", xaml);
        Assert.Contains("<DataTrigger Binding=\"{Binding IsDashboardSpendingAmountGateLocked}\" Value=\"True\">", xaml);
        Assert.Contains("<Setter Property=\"IsEnabled\" Value=\"False\" />", xaml);
    }

    [Fact]
    public void PopupBindsToMainWindowViewModelForLockedState()
    {
        var source = File.ReadAllText(PopupCodeBehindPath);

        Assert.Contains("QuickAddPopup(MainVM mainViewModel)", source);
        Assert.Contains("DataContext = mainViewModel;", source);
    }
}
