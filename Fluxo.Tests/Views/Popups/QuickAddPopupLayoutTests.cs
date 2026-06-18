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
    public void SufficientFundsActionGate_DisablesOnlyFundDependentTiles()
    {
        var xaml = File.ReadAllText(PopupXamlPath);

        Assert.Contains("PopupTitle=\"Quick Access\"", xaml);
        Assert.Contains("Columns=\"3\"", xaml);
        Assert.Contains("x:Key=\"LockedDisabledQuickAddTileButtonStyle\"", xaml);
        Assert.Contains("x:Name=\"NewAccountQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"NewTransactionQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"NewSavingGoalQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"RunSetupWizardQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"CheckForUpdatesQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"NewRecurringTransactionQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"ViewAccountsQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"StartPlanningModeQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"OpenSettingsQuickAddButton\"", xaml);
        Assert.Equal(4, xaml.Split("Style=\"{StaticResource LockedDisabledQuickAddTileButtonStyle}\"").Length - 1);
        Assert.Contains("Style=\"{StaticResource QuickAddTileButtonStyle}\"", xaml);
        Assert.Contains("<DataTrigger Binding=\"{Binding IsSufficientFundsActionGateLocked}\" Value=\"True\">", xaml);
        Assert.Contains("<Setter Property=\"IsEnabled\" Value=\"False\" />", xaml);
    }

    [Fact]
    public void PopupBindsToMainWindowViewModelForLockedState()
    {
        var source = File.ReadAllText(PopupCodeBehindPath);

        Assert.Contains("QuickAddPopup(MainVM mainViewModel)", source);
        Assert.Contains("DataContext = mainViewModel;", source);
    }

    [Fact]
    public void PopupRoutesNewActionsThroughMainWindow()
    {
        var source = File.ReadAllText(PopupCodeBehindPath);

        Assert.Contains("OnRunSetupWizardClick", source);
        Assert.Contains("mainWindow.OpenQuickSetupWizardPopup()", source);
        Assert.Contains("OnCheckForUpdatesClick", source);
        Assert.Contains("mainWindow.CheckForUpdatesFromQuickAccessAsync()", source);
        Assert.Contains("OnNewRecurringTransactionClick", source);
        Assert.Contains("mainWindow.OpenRecurringAddNewTransactionPopup()", source);
        Assert.Contains("OnViewAccountsClick", source);
        Assert.Contains("mainWindow.OpenAccountsListPopup()", source);
        Assert.Contains("OnStartPlanningModeClick", source);
        Assert.Contains("mainWindow.OpenPlanningPopup()", source);
        Assert.Contains("OnOpenSettingsClick", source);
        Assert.Contains("mainWindow.OpenSettingsPopup()", source);
    }
}
