using System.IO;
using System.Windows;
using System.Windows.Controls;
using Fluxo.Resources.CustomControls;
using Fluxo.Tests.TestSupport;
using Fluxo.Views.Popups;
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
    public void QuickAccess_ShowsSetupUpdateAndSettingsInSecondRow()
    {
        RunOnStaThread(() =>
        {
            EnsureApplicationResources();
            var popup = new QuickAddPopup(null!);
            var grid = Assert.IsType<SpacedUniformGrid>(popup.Content);
            var secondRow = grid.Children.Cast<Button>().Skip(3).Take(3)
                .Select(button => GetTileLabel(button)).ToArray();

            Assert.Equal(["Run Quick Setup", "Check For Updates", "Open Settings"], secondRow);
            Assert.Null(popup.FindName("NewRecurringTransactionQuickAddButton"));
        });
    }

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
        Assert.Contains("x:Name=\"ViewAccountsQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"PlanningReportQuickAddButton\"", xaml);
        Assert.Contains("x:Name=\"BudgetForecastQuickAddButton\"", xaml);
        Assert.Contains("Path=\"{StaticResource CalendarFuture}\"", xaml);
        Assert.Contains("Text=\"Budget Forecast\"", xaml);
        Assert.Contains("x:Name=\"OpenSettingsQuickAddButton\"", xaml);
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
        Assert.Contains("OnViewAccountsClick", source);
        Assert.Contains("mainWindow.OpenAccountsListPopup()", source);
        Assert.Contains("OnPlanningReportClick", source);
        Assert.Contains("mainWindow.OpenPlanningReport()", source);
        Assert.Contains("OnBudgetForecastClick", source);
        Assert.Contains("mainWindow.OpenBudgetForecast()", source);
        Assert.Contains("OnOpenSettingsClick", source);
        Assert.Contains("mainWindow.OpenSettingsPopup()", source);
    }

    private static string GetTileLabel(Button button)
    {
        var content = Assert.IsType<StackPanel>(button.Content);
        return Assert.IsType<TextBlock>(content.Children[1]).Text;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception caught) { exception = caught; }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null) throw exception;
    }

    private static void EnsureApplicationResources()
    {
        var application = Application.Current ?? new Application();
        foreach (var resource in new[]
                 {
                     "Theme.xaml", "Fonts.xaml", "Icons.xaml", "Converters.xaml",
                     "Styles/ContainerStyles.xaml", "Styles/ButtonStyles.xaml", "Styles/TextBoxStyles.xaml",
                     "Styles/GlobalStyles.xaml", "Styles/PopupStyles.xaml", "Styles/MainWindowStyles.xaml",
                     "Styles/StepNavigatorStyle.xaml", "Styles/QuickSetupWizardStyle.xaml"
                 })
        {
            if (application.Resources.MergedDictionaries.Any(dictionary =>
                    dictionary.Source?.OriginalString.EndsWith($"Resources/{resource}", StringComparison.OrdinalIgnoreCase) == true))
                continue;

            application.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri($"/Fluxo.Resources;component/Resources/{resource}", UriKind.Relative)
            });
        }
    }
}
