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
    public void HostedPageNavigation_KeepsToastVisibleUntilCrossfadeCompletes()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));
        var navigationBody = ExtractMethodBodyBySignature(source, "private async Task NavigateToHostedPageAsync(HostedMainPage page)");

        var toastIndex = navigationBody.IndexOf("await _dialogService.ShowToastWhileAsync(", StringComparison.Ordinal);
        var prepareIndex = navigationBody.IndexOf("nextPage = await PrepareHostedPageContentAsync(page);", StringComparison.Ordinal);
        var crossfadeIndex = navigationBody.IndexOf("await CrossfadeToHostedPageAsync(nextPage);", StringComparison.Ordinal);
        var activePageIndex = navigationBody.IndexOf("_activeHostedPage = page;", StringComparison.Ordinal);

        Assert.True(toastIndex >= 0, "Hosted page navigation should wrap page preparation and crossfade in the toast.");
        Assert.True(prepareIndex > toastIndex, "Hosted page preparation should run while the toast is visible.");
        Assert.True(crossfadeIndex > prepareIndex, "Hosted page crossfade should complete while the toast is visible.");
        Assert.True(activePageIndex > crossfadeIndex, "The active hosted page should update after the fade-in completes.");
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

        Assert.Contains("UpdateHeaderDateSelectorEnabledState(page);", source);
        Assert.Contains("DaySpinnerControlHost.IsEnabled = page is not HostedMainPage.Analytics and not HostedMainPage.Calendar;", source);
        Assert.DoesNotContain("DaySpinnerControlHost.IsHitTestVisible = page is not HostedMainPage.Analytics and not HostedMainPage.Calendar;", source);
        Assert.Contains("UpdateHeaderDateSelectorEnabledState(null);", source);
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
