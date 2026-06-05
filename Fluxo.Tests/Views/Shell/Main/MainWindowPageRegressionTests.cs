using System.IO;
using System.Linq;
using System.Xml.Linq;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowPageRegressionTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void MainPages_AreStoredUnderPagesFolder()
    {
        foreach (var pageName in new[] { "Dashboard", "Analytics", "Calendar", "Ledger" })
        {
            Assert.False(File.Exists(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", $"{pageName}.xaml")));
            Assert.False(File.Exists(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", $"{pageName}.xaml.cs")));
            Assert.True(File.Exists(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", $"{pageName}.xaml")));
            Assert.True(File.Exists(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", $"{pageName}.xaml.cs")));
        }
    }

    [Fact]
    public void PageSwitching_UsesCapitalizedToastMessagesIncludingDashboard()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));

        Assert.Contains("\"Loading Dashboard\"", source);
        Assert.Contains("\"Loading Analytics\"", source);
        Assert.Contains("\"Loading Calendar\"", source);
        Assert.Contains("\"Loading Ledger\"", source);
        Assert.DoesNotContain("\"Loading analytics\"", source);
        Assert.DoesNotContain("\"Loading calendar\"", source);
        Assert.DoesNotContain("\"Loading ledger\"", source);
    }

    [Fact]
    public void MainPageNavigation_KeepsToastVisibleUntilTransitionAndPreparationComplete()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));
        var navigationBody = ExtractMethodBodyBySignature(source, "private async Task NavigateToMainPageAsync(MainPage page)");

        var toastIndex = navigationBody.IndexOf("await _dialogService.ShowToastWhileAsync(", StringComparison.Ordinal);
        var resolveIndex = navigationBody.IndexOf("nextPage = ResolveMainPageView(page);", StringComparison.Ordinal);
        var transitionIndex = navigationBody.IndexOf("await TransitionToMainPageAsync(nextPage);", StringComparison.Ordinal);
        var prepareIndex = navigationBody.IndexOf("await PrepareMainPageContentAsync(page);", StringComparison.Ordinal);
        var activePageIndex = navigationBody.IndexOf("_activeMainPage = page;", StringComparison.Ordinal);

        Assert.True(toastIndex >= 0, "Main page navigation should wrap page resolution, transition, preparation, and final state updates in the toast.");
        Assert.True(resolveIndex > toastIndex, "The next page view should be resolved while the toast is visible.");
        Assert.True(transitionIndex > resolveIndex, "The visual transition should run before page preparation.");
        Assert.True(prepareIndex > transitionIndex, "Page preparation should run after the new page has faded in.");
        Assert.True(activePageIndex > prepareIndex, "The active main page should update only after preparation finishes.");
    }

    [Fact]
    public void MainPageNavigation_KeepsCurrentNavigationButtonCheckedUntilPreparationCompletes()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));
        var navigationBody = ExtractMethodBodyBySignature(source, "private async Task NavigateToMainPageAsync(MainPage page)");

        var prepareFlagIndex = navigationBody.IndexOf("_isPreparingMainPage = true;", StringComparison.Ordinal);
        var holdCurrentPageIndex = navigationBody.LastIndexOf(
            "UpdateMainNavigationCheckedState(_activeMainPage);",
            prepareFlagIndex,
            StringComparison.Ordinal);
        var toastIndex = navigationBody.IndexOf("await _dialogService.ShowToastWhileAsync(", StringComparison.Ordinal);
        var transitionIndex = navigationBody.IndexOf("await TransitionToMainPageAsync(nextPage);", StringComparison.Ordinal);
        var prepareIndex = navigationBody.IndexOf("await PrepareMainPageContentAsync(page);", StringComparison.Ordinal);
        var activePageIndex = navigationBody.IndexOf("_activeMainPage = page;", StringComparison.Ordinal);
        var switchNavigationIndex = navigationBody.IndexOf(
            "UpdateMainNavigationCheckedState(_activeMainPage);",
            activePageIndex,
            StringComparison.Ordinal);

        Assert.True(holdCurrentPageIndex >= 0, "A valid navigation click should be reset to the current page before loading starts.");
        Assert.True(holdCurrentPageIndex < prepareFlagIndex, "The side navigation should hold the current page before preparation starts.");
        Assert.True(switchNavigationIndex > activePageIndex, "The side navigation should switch after the active page changes.");
        Assert.True(switchNavigationIndex > transitionIndex, "The side navigation should switch only after the new page has faded in.");
        Assert.True(switchNavigationIndex > prepareIndex, "The side navigation should switch only after page preparation finishes.");
        Assert.True(switchNavigationIndex > toastIndex, "The final navigation state update should happen inside the toast callback before it closes.");
    }

    [Fact]
    public void Dashboard_UsesSharedMainPageTransitionPipeline()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));
        var pageEnumBody = ExtractMethodBodyBySignature(source, "private enum MainPage");
        var navigationBody = ExtractMethodBodyBySignature(source, "private async Task NavigateToMainPageAsync(MainPage page)");
        var prepareBody = ExtractMethodBodyBySignature(source, "private async Task PrepareMainPageContentAsync(MainPage page)");

        Assert.Contains("Dashboard", pageEnumBody);
        Assert.Contains("case MainPage.Dashboard:", prepareBody);
        Assert.Contains("ResolveMainPageView(page)", navigationBody);
        Assert.Contains("TransitionToMainPageAsync(nextPage)", navigationBody);
        Assert.Contains("PrepareMainPageContentAsync(page)", navigationBody);
        Assert.DoesNotContain("ShowDashboardShellAsync", source);
        Assert.DoesNotContain("CrossfadeToDashboardShellAsync", source);
        Assert.DoesNotContain("PrepareHostedPageContentAsync", source);
        Assert.DoesNotContain("NavigateToHostedPageAsync", source);
        Assert.DoesNotContain("DashboardPageHost", source);
        Assert.DoesNotContain("ResolveMainPageHost", source);
        Assert.DoesNotContain("CollapseInactiveMainPageHost", source);
    }

    [Fact]
    public void PopupOverlay_BlursAndClearsCurrentPageHost()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));
        var applyPopupBlur = ExtractMethodBodyBySignature(source, "private void ApplyPopupBlur()");
        var clearPopupBlur = ExtractMethodBodyBySignature(source, "private void ClearPopupBlur()");

        Assert.Contains("MainPageHost.Effect = CreatePopupBlurEffect();", applyPopupBlur);
        Assert.Contains("MainPageHost.Effect = null;", clearPopupBlur);
    }

    [Fact]
    public void MainWindow_DisablesHeaderDateSelectorForAnalyticsAndCalendar()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));

        Assert.Contains("UpdateHeaderDateSelectorEnabledState(_activeMainPage);", source);
        Assert.Contains("DaySpinnerControlHost.IsEnabled = page is not MainPage.Analytics and not MainPage.Calendar;", source);
        Assert.DoesNotContain("DaySpinnerControlHost.IsHitTestVisible = page is not MainPage.Analytics and not MainPage.Calendar;", source);
        Assert.DoesNotContain("UpdateHeaderDateSelectorEnabledState(null);", source);
    }

    [Fact]
    public void DaySpinnerControl_DisabledStateDimsWithoutReplacingTransparentBackground()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Controls", "DaySpinnerControl.xaml"));

        Assert.Contains("Background=\"Transparent\"", xaml);
        Assert.Contains("Binding=\"{Binding IsEnabled, RelativeSource={RelativeSource AncestorType=UserControl}}\" Value=\"False\"", xaml);
        Assert.Contains("Property=\"Opacity\" Value=\"0.4\"", xaml);
    }

    [Fact]
    public void AnalyticsAndLedger_ExposeStartAndEndDateSelectors()
    {
        AssertPageHasDateSelectors("Analytics", "AnalyticsStartDateSelector", "StartDate", "AnalyticsEndDateSelector", "EndDate");
        AssertPageHasDateSelectors("Ledger", "LedgerStartDateSelector", "StartDate", "LedgerEndDateSelector", "EndDate");
    }

    [Fact]
    public void Ledger_UsesPeriodAwareEmptyPlaceholder()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var ledgerVm = File.ReadAllText(RepositoryPaths.File("Fluxo", "ViewModels", "Shell", "Main", "LedgerVM.cs"));

        Assert.Contains("No ledger data found for", ledgerXaml);
        Assert.Contains("Text=\"{Binding EmptyStatePeriodText, Mode=OneWay}\"", ledgerXaml);
        Assert.Contains("public string EmptyStatePeriodText", ledgerVm);
        Assert.Contains("DateRangeResolver.Resolve", ledgerVm);
    }

    [Fact]
    public void MainViewModeToggle_BindsSelectedValueForInitialDaySelection()
    {
        var toggleXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Controls", "MainViewModeToggleControl.xaml"));

        Assert.Contains("SelectedValue=\"{Binding SelectedMainContentViewMode, Mode=OneWay}\"", toggleXaml);
        Assert.Contains("Value=\"{x:Static enums:MainContentViewMode.Daily}\"", toggleXaml);
    }

    [Fact]
    public void MainVM_DoesNotExposeSharedViewModeToggle()
    {
        var mainVm = File.ReadAllText(RepositoryPaths.File("Fluxo", "ViewModels", "Shell", "Main", "MainVM.cs"));
        var services = File.ReadAllText(RepositoryPaths.File("Fluxo", "Extensions", "ServiceCollectionExtensions.cs"));

        Assert.DoesNotContain("ViewModeToggle =>", mainVm);
        Assert.DoesNotContain("AddSingleton<MainViewModeToggleVM>", services);
        Assert.Contains("AddTransient<MainViewModeToggleVM>", services);
    }

    [Fact]
    public void Ledger_UsesOwnViewModeToggleAndDisablesFiltersWhenNoTransactions()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));

        Assert.Contains("DataContext=\"{Binding ViewModeToggle}\"", ledgerXaml);
        Assert.DoesNotContain("DataContext=\"{Binding DataContext.ViewModeToggle, RelativeSource={RelativeSource AncestorType=Window}}\"", ledgerXaml);
        Assert.Contains("x:Name=\"LedgerFiltersRow\"", ledgerXaml);
        Assert.Contains("IsEnabled=\"{Binding HasTransactions}\"", ledgerXaml);
    }

    [Fact]
    public void Ledger_GroupByDisplaysSpendingSourcesWithSpace()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var ledgerVm = File.ReadAllText(RepositoryPaths.File("Fluxo", "ViewModels", "Shell", "Main", "LedgerVM.cs"));

        Assert.Contains("LedgerGroupingModeDisplayConverter", ledgerXaml);
        Assert.Contains("Spending Sources", ledgerVm);
    }

    [Fact]
    public void Ledger_FilterDropdownsApplyOnCloseButGroupByDoesNot()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));

        Assert.Equal(4, CountOccurrences(ledgerXaml, "DropDownClosed=\"OnFilterDropDownClosed\""));
        Assert.Contains("ItemsSource=\"{Binding TypeFilters}\"", ledgerXaml);
        Assert.Contains("ItemsSource=\"{Binding SpendingSourceFilters}\"", ledgerXaml);
        Assert.Contains("ItemsSource=\"{Binding CategoryFilters}\"", ledgerXaml);
        Assert.Contains("ItemsSource=\"{Binding TagFilters}\"", ledgerXaml);

        var groupingComboIndex = ledgerXaml.IndexOf("x:Name=\"LedgerGroupingComboBox\"", StringComparison.Ordinal);
        Assert.True(groupingComboIndex >= 0);
        var groupingComboEndIndex = ledgerXaml.IndexOf("/>", groupingComboIndex, StringComparison.Ordinal);
        Assert.True(groupingComboEndIndex > groupingComboIndex);
        var groupingCombo = ledgerXaml.Substring(groupingComboIndex, groupingComboEndIndex - groupingComboIndex);
        Assert.DoesNotContain("OnFilterDropDownClosed", groupingCombo);
    }

    [Fact]
    public void Ledger_ClearFiltersButtonUsesBanIconAndClearHandler()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var banButtonIndex = ledgerXaml.IndexOf("ButtonIcon=\"{StaticResource Ban}\"", StringComparison.Ordinal);

        Assert.True(banButtonIndex >= 0);
        var buttonEndIndex = ledgerXaml.IndexOf("/>", banButtonIndex, StringComparison.Ordinal);
        Assert.True(buttonEndIndex > banButtonIndex);
        var button = ledgerXaml.Substring(banButtonIndex, buttonEndIndex - banButtonIndex);
        Assert.Contains("Click=\"OnClearFiltersClick\"", button);
        Assert.DoesNotContain("Click=\"OnApplyFiltersClick\"", button);
    }

    private static void AssertPageHasDateSelectors(
        string pageName,
        string startSelectorName,
        string startBinding,
        string endSelectorName,
        string endBinding)
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", $"{pageName}.xaml"));
        var document = XDocument.Parse(xaml);
        var dateSelectors = document
            .Descendants()
            .Where(element => element.Name.LocalName == "DateSelector")
            .ToList();

        Assert.Equal(2, dateSelectors.Count);
        Assert.Contains(dateSelectors, selector =>
            (string?)selector.Attribute(XamlNamespace + "Name") == startSelectorName &&
            ((string?)selector.Attribute("SelectedDate"))?.Contains(startBinding) is true);
        Assert.Contains(dateSelectors, selector =>
            (string?)selector.Attribute(XamlNamespace + "Name") == endSelectorName &&
            ((string?)selector.Attribute("SelectedDate"))?.Contains(endBinding) is true);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ExtractMethodBodyBySignature(string source, string signatureMarker)
    {
        var signatureIndex = source.IndexOf(signatureMarker, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature '{signatureMarker}' was not found in MainWindow.xaml.cs.");

        var openingBraceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(openingBraceIndex >= 0, $"Opening brace for method signature '{signatureMarker}' was not found.");

        var depth = 0;
        for (var index = openingBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
                continue;
            }

            if (source[index] != '}')
                continue;

            depth--;
            if (depth != 0)
                continue;

            return source.Substring(openingBraceIndex + 1, index - openingBraceIndex - 1);
        }

        throw new InvalidOperationException($"Closing brace for method signature '{signatureMarker}' was not found.");
    }
}
