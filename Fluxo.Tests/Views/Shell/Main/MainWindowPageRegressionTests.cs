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
    public void OrdinaryAnalyticsNavigation_PreservesAnalyticsDateSelection()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));
        var prepareBody = ExtractMethodBodyBySignature(source, "private async Task PrepareMainPageContentAsync(MainPage page)");
        var analyticsStart = prepareBody.IndexOf("case MainPage.Analytics:", StringComparison.Ordinal);
        var calendarStart = prepareBody.IndexOf("case MainPage.Calendar:", analyticsStart, StringComparison.Ordinal);
        var analyticsCase = prepareBody[analyticsStart..calendarStart];

        Assert.DoesNotContain("ApplyOpenRange", analyticsCase);
        Assert.Contains("PrepareForOpenAsync(showInternalToast: false)", analyticsCase);
    }

    [Fact]
    public void DaySelector_IsCollapsedOutsideDashboard()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));

        Assert.Contains("DaySpinnerControlHost.Visibility = page == MainPage.Dashboard", source);
        Assert.Contains("? Visibility.Visible", source);
        Assert.Contains(": Visibility.Collapsed", source);
    }

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
    public void MainWindow_HidesHeaderDateSelectorOutsideDashboard()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));

        Assert.Contains("UpdateHeaderDaySelectorVisibility(_activeMainPage);", source);
        Assert.Contains("DaySpinnerControlHost.Visibility = page == MainPage.Dashboard", source);
        Assert.DoesNotContain("UpdateHeaderDateSelectorEnabledState", source);
    }

    [Fact]
    public void MainWindow_ConfiguresHeaderDaySpinnerFutureNavigationByPage()
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));

        Assert.Contains("UpdateHeaderDaySpinnerPagePolicy(_activeMainPage);", source);
        Assert.Contains("private void UpdateHeaderDaySpinnerPagePolicy(MainPage page)", source);
        Assert.Contains("_mainVM.DaySpinner.AllowFuturePeriodNavigation = page == MainPage.Dashboard;", source);
        Assert.Contains("UpdateHeaderDaySelectorVisibility(_activeMainPage);", source);
    }

    [Fact]
    public void MainWindow_HeaderBindsToActivePageTitleInsteadOfUsernameGreeting()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml"));
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));

        Assert.Contains("Text=\"{Binding ActivePageTitle", xaml);
        Assert.DoesNotContain("Welcome back,", xaml);
        Assert.DoesNotContain("Text=\"{Binding Username}\"", xaml);
        Assert.Contains("RefreshActivePageTitle();", source);
        Assert.Contains("ActivePageTitle = _mainVM.IsAppLocked ? \"Locked\" : GetMainPageTitle(_activeMainPage);", source);
        Assert.Contains("=> page switch", source);
        Assert.Contains("MainPage.Dashboard => \"Dashboard\"", source);
        Assert.Contains("MainPage.Analytics => \"Analytics\"", source);
        Assert.Contains("MainPage.Calendar => \"Calendar\"", source);
        Assert.Contains("MainPage.Ledger => \"Ledger\"", source);
    }

    [Fact]
    public void NotificationPanel_RendersCountBadgeBesideGroupHeader()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Sections", "NotificationPanel.xaml"));

        Assert.Contains("x:Name=\"NotificationCountBadge\"", xaml);
        Assert.Contains("Background=\"{StaticResource Brush.Mint}\"", xaml);
        Assert.Contains("Foreground=\"{StaticResource Brush.Text.Primary.Dark}\"", xaml);
        Assert.Contains("Text=\"{Binding Count}\"", xaml);
        Assert.Contains("Text=\"{Binding Header}\"", xaml);
    }

    [Fact]
    public void DaySpinnerControl_DisabledStateDimsWithoutReplacingTransparentBackground()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Controls", "DaySpinnerControl.xaml"));

        Assert.Contains("Background=\"Transparent\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding IsSpinnerEnabled}\"", xaml);
        Assert.Contains("Binding=\"{Binding IsEnabled, RelativeSource={RelativeSource AncestorType=UserControl}}\" Value=\"False\"", xaml);
        Assert.Contains("Property=\"Opacity\" Value=\"0.4\"", xaml);
        Assert.DoesNotContain("Visibility=\"{Binding IsSpinnerVisible", xaml);
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
    public void Ledger_ViewModeToggleIsTopOverlayAndContentUsesFixedTopOffset()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var rootGrid = ExtractSection(ledgerXaml, "<Grid Margin=\"24,0,24,0\">", "</UserControl>");
        var toggleIndex = rootGrid.IndexOf("<controls:MainViewModeToggleControl", StringComparison.Ordinal);
        var contentIndex = rootGrid.IndexOf("x:Name=\"LedgerContentGrid\"", StringComparison.Ordinal);

        Assert.True(toggleIndex >= 0);
        Assert.True(contentIndex > toggleIndex);
        Assert.Contains("HorizontalAlignment=\"Center\"", rootGrid);
        Assert.Contains("VerticalAlignment=\"Top\"", rootGrid);
        Assert.Contains("x:Name=\"LedgerContentGrid\"", rootGrid);
        Assert.Contains("Margin=\"0,48,0,0\"", rootGrid);
        Assert.DoesNotContain("Margin=\"0,16\"", rootGrid);
    }

    [Fact]
    public void Ledger_GroupByDisplaysAccountsWithSpace()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var ledgerVm = File.ReadAllText(RepositoryPaths.File("Fluxo", "ViewModels", "Shell", "Main", "LedgerVM.cs"));

        Assert.Contains("LedgerGroupingModeDisplayConverter", ledgerXaml);
        Assert.Contains("Accounts", ledgerVm);
    }

    [Fact]
    public void Ledger_FilterDropdownsApplyOnCloseButGroupByDoesNot()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var ledgerCodeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml.cs"));

        Assert.Equal(4, CountOccurrences(ledgerXaml, "DropDownClosed=\"OnFilterDropDownClosed\""));
        Assert.Contains("ItemsSource=\"{Binding TypeFilters}\"", ledgerXaml);
        Assert.Contains("ItemsSource=\"{Binding AccountFilters}\"", ledgerXaml);
        Assert.Contains("ItemsSource=\"{Binding CategoryFilters}\"", ledgerXaml);
        Assert.Contains("ItemsSource=\"{Binding TagFilters}\"", ledgerXaml);
        Assert.Contains("StaysOpen=\"False\"", ledgerXaml);
        Assert.Contains("comboBox.IsDropDownOpen = true;", ledgerCodeBehind);
        Assert.Contains("_suppressNextFilterDropDownClose", ledgerCodeBehind);
        Assert.Contains("PreviewMouseLeftButtonUp", ledgerXaml);

        var groupingComboIndex = ledgerXaml.IndexOf("x:Name=\"LedgerGroupingComboBox\"", StringComparison.Ordinal);
        Assert.True(groupingComboIndex >= 0);
        var groupingComboEndIndex = ledgerXaml.IndexOf("/>", groupingComboIndex, StringComparison.Ordinal);
        Assert.True(groupingComboEndIndex > groupingComboIndex);
        var groupingCombo = ledgerXaml.Substring(groupingComboIndex, groupingComboEndIndex - groupingComboIndex);
        Assert.DoesNotContain("OnFilterDropDownClosed", groupingCombo);
    }

    [Fact]
    public void Ledger_FilterComboTogglesShowSelectionBadgeAndTooltip()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var filterComboStyleSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerFilterComboStyle\"", "x:Key=\"LedgerGroupingComboStyle\"");

        Assert.Contains("x:Name=\"SelectionCountBadge\"", filterComboStyleSection);
        Assert.Contains("Background=\"{StaticResource Brush.Mint}\"", filterComboStyleSection);
        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", filterComboStyleSection);
        Assert.Contains("ElementName=\"SelectionCountBadge\"", filterComboStyleSection);
        Assert.DoesNotContain("<MultiBinding Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\">", filterComboStyleSection);
        Assert.Contains("Brush.Text.Primary", filterComboStyleSection);
        Assert.Contains("Text=\"{Binding Tag.SelectionCount", filterComboStyleSection);
        Assert.Contains("ToolTip=\"{Binding Tag.SelectionToolTip", filterComboStyleSection);
        Assert.Contains("TargetName=\"SelectionCountBadge\" Property=\"Visibility\" Value=\"Collapsed\"", filterComboStyleSection);
    }

    [Fact]
    public void Ledger_GroupingComboDoesNotUseFilterSelectionBadgeOrTooltip()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var groupingComboStyleSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerGroupingComboStyle\"", "x:Key=\"LedgerFilterOptionTemplate\"");

        Assert.DoesNotContain("SelectionCountBadge", groupingComboStyleSection);
        Assert.DoesNotContain("SelectionToolTip", groupingComboStyleSection);
        Assert.Contains("Text=\"{Binding Tag", groupingComboStyleSection);
    }

    [Fact]
    public void Ledger_ClearFiltersButtonUsesBanIconAndClearHandler()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var banButtonIndex = ledgerXaml.IndexOf("Click=\"OnClearFiltersClick\"", StringComparison.Ordinal);

        Assert.True(banButtonIndex >= 0);
        var buttonStartIndex = ledgerXaml.LastIndexOf("<customControls:BalloonButton", banButtonIndex, StringComparison.Ordinal);
        var buttonEndIndex = ledgerXaml.IndexOf("/>", banButtonIndex, StringComparison.Ordinal);
        Assert.True(buttonStartIndex >= 0);
        Assert.True(buttonEndIndex > buttonStartIndex);
        var button = ledgerXaml.Substring(buttonStartIndex, buttonEndIndex - buttonStartIndex);
        Assert.Contains("ButtonIcon=\"{StaticResource Ban}\"", button);
        Assert.Contains("Click=\"OnClearFiltersClick\"", button);
        Assert.DoesNotContain("Click=\"OnApplyFiltersClick\"", button);
    }

    [Fact]
    public void Ledger_ExportButtonUsesVisibleRowsAndToastCsvHandler()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var ledgerCodeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml.cs"));
        var ledgerCsvExport = File.ReadAllText(RepositoryPaths.File("Fluxo", "ViewModels", "Shell", "Main", "LedgerCsvExport.cs"));
        var exportButtonIndex = ledgerXaml.IndexOf("ButtonText=\"Export Data\"", StringComparison.Ordinal);

        Assert.True(exportButtonIndex >= 0);
        var buttonEndIndex = ledgerXaml.IndexOf("/>", exportButtonIndex, StringComparison.Ordinal);
        Assert.True(buttonEndIndex > exportButtonIndex);
        var button = ledgerXaml.Substring(exportButtonIndex, buttonEndIndex - exportButtonIndex);
        Assert.Contains("Click=\"OnExportDataClick\"", button);
        Assert.Contains("IsEnabled=\"{Binding HasVisibleTransactions}\"", button);

        Assert.Contains("SaveFileDialog", ledgerCodeBehind);
        Assert.Contains("fluxo_Ledger_", ledgerCsvExport);
        Assert.Contains("yyyyMMdd-hhmmss", ledgerCsvExport);
        Assert.Contains("\"Exporting ledger data...\"", ledgerCodeBehind);
        Assert.Contains("GetVisibleTransactionsForExport", ledgerCodeBehind);
        Assert.Contains("LedgerCsvExport.BuildBytes", ledgerCodeBehind);
    }

    [Fact]
    public void Ledger_UsesRequestedDisabledHoverAndDeleteDialogStates()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var ledgerCodeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml.cs"));
        var filterComboStyleSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerFilterComboStyle\"", "x:Key=\"LedgerGroupingComboStyle\"");
        var groupingComboStyleSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerGroupingComboStyle\"", "x:Key=\"LedgerFilterOptionTemplate\"");
        var rowTemplateSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerRowTemplate\"", "x:Key=\"LedgerGroupItemStyle\"");

        Assert.Contains("Property=\"IsEnabled\" Value=\"False\"", filterComboStyleSection);
        Assert.Contains("Property=\"Opacity\" Value=\"0.4\"", filterComboStyleSection);
        Assert.Contains("Property=\"IsEnabled\" Value=\"False\"", groupingComboStyleSection);
        Assert.Contains("Property=\"Opacity\" Value=\"0.4\"", groupingComboStyleSection);
        Assert.Contains("Brush.Background.Hover", rowTemplateSection);
        Assert.Contains("IsDisabledByAnotherEdit", rowTemplateSection);
        Assert.Contains("Property=\"IsHitTestVisible\" Value=\"False\"", rowTemplateSection);
        Assert.Contains("CommandParameter=\"{Binding}\"", rowTemplateSection);
        Assert.Contains("CreateDuplicateTransactionDraft", ledgerCodeBehind);
        Assert.Contains("ButtonIcon=\"{StaticResource CloneRegular}\"", rowTemplateSection);
        Assert.Contains("IsEnabled=\"{Binding IsEditing, Converter={StaticResource BoolNegationConverter}}\"", rowTemplateSection);
        Assert.Contains("CheckedIcon=\"{StaticResource Ban}\"", rowTemplateSection);
        Assert.Contains("OnDeleteOrDiscardTransactionClick", rowTemplateSection);
        Assert.Contains("Storyboard.TargetProperty=\"(UIElement.RenderTransform).(TranslateTransform.X)\"", rowTemplateSection);
        Assert.Contains("To=\"3\"", rowTemplateSection);
        Assert.Contains("FluxoMessageBox.Show", ledgerCodeBehind);
        Assert.DoesNotContain("= MessageBox.Show", ledgerCodeBehind);
    }

    [Fact]
    public void Ledger_RowTemplateExpandsChildTransactionsUnderParentRows()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var ledgerCodeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml.cs"));
        var rowTemplateSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerRowTemplate\"", "x:Key=\"LedgerGroupItemStyle\"");

        Assert.Contains("Loaded=\"OnLedgerRowLoaded\"", rowTemplateSection);
        Assert.Contains("PreviewMouseLeftButtonDownEvent", ledgerCodeBehind);
        Assert.Contains("handledEventsToo: true", ledgerCodeBehind);
        Assert.Contains("ItemsSource=\"{Binding ChildTransactions}\"", rowTemplateSection);
        Assert.Contains("Visibility=\"{Binding IsChildrenExpanded, Converter={StaticResource BoolToVisibilityConverter}}\"", rowTemplateSection);
        Assert.Contains("LedgerChildRowTemplate", rowTemplateSection);
        Assert.Contains("e.Handled = true;", ledgerCodeBehind);
        Assert.Contains("ToggleChildTransactionsCommand", ledgerCodeBehind);
    }

    [Fact]
    public void Ledger_ParentRowsShowChildTransactionCountImmediatelyAfterName()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var rowTemplateSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerRowTemplate\"", "x:Key=\"LedgerGroupItemStyle\"");

        var nameEditorIndex = rowTemplateSection.IndexOf("Text=\"{Binding Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}\"", StringComparison.Ordinal);
        var countBadgeIndex = rowTemplateSection.IndexOf("x:Name=\"LedgerChildCountBadge\"", StringComparison.Ordinal);

        Assert.True(nameEditorIndex >= 0);
        Assert.True(countBadgeIndex > nameEditorIndex);
        Assert.Contains("Text=\"{Binding ChildTransactions.Count}\"", rowTemplateSection);
        Assert.Contains("Visibility=\"{Binding HasChildTransactions, Converter={StaticResource BoolToVisibilityConverter}}\"", rowTemplateSection);
    }

    [Fact]
    public void Ledger_ChildRowsKeepIndicatorIndentButAlignDataColumnsAndUseHoverAnimation()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var ledgerCodeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml.cs"));
        var childTemplateSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerChildRowTemplate\"", "x:Key=\"LedgerRowTemplate\"");
        var rowTemplateSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerRowTemplate\"", "x:Key=\"LedgerGroupItemStyle\"");

        Assert.Contains("Margin=\"0,0,0,8\"", rowTemplateSection);
        Assert.Contains("x:Name=\"ChildRowNameContent\"", childTemplateSection);
        Assert.Contains("x:Name=\"ChildRowBatchCheckBox\"", childTemplateSection);
        Assert.Contains("IsChecked=\"{Binding IsSelectedForBatch, Mode=OneWay}\"", childTemplateSection);
        Assert.Contains("IsEnabled=\"False\"", childTemplateSection);
        Assert.Contains("x:Name=\"ChildRowTagCell\"", childTemplateSection);
        Assert.Contains("x:Name=\"ChildRowAccountCell\"", childTemplateSection);
        Assert.Contains("x:Name=\"ChildRowSignedAmountText\"", childTemplateSection);
        Assert.Contains("x:Name=\"ChildRowMoneyEditor\"", childTemplateSection);
        Assert.Contains("x:Name=\"ChildRowRoot\"", childTemplateSection);
        Assert.DoesNotContain("Width=\"{Binding ActualWidth", childTemplateSection);
        Assert.Contains("x:Name=\"ChildRowSurface\"", childTemplateSection);
        Assert.Equal(6, CountOccurrences(childTemplateSection, "<TranslateTransform />"));
        Assert.Contains("Margin=\"0,8,8,8\"", childTemplateSection);
        Assert.DoesNotContain("Margin=\"-26,0,0,0\"", childTemplateSection);
        Assert.DoesNotContain("Margin=\"-23,0,0,0\"", childTemplateSection);
        Assert.DoesNotContain("Margin=\"0,8,26,8\"", childTemplateSection);
        Assert.Contains("Brush.Background.Hover", childTemplateSection);
        Assert.Contains("Storyboard.TargetName=\"ChildRowNameContent\"", childTemplateSection);
        Assert.Contains("Storyboard.TargetName=\"ChildRowBatchCheckBox\"", childTemplateSection);
        Assert.Contains("Storyboard.TargetName=\"ChildRowTagCell\"", childTemplateSection);
        Assert.Contains("Storyboard.TargetName=\"ChildRowAccountCell\"", childTemplateSection);
        Assert.Contains("Storyboard.TargetName=\"ChildRowSignedAmountText\"", childTemplateSection);
        Assert.Contains("Storyboard.TargetName=\"ChildRowMoneyEditor\"", childTemplateSection);
        Assert.Contains("Storyboard.TargetProperty=\"(UIElement.RenderTransform).(TranslateTransform.X)\"", childTemplateSection);
        Assert.Contains("To=\"3\"", childTemplateSection);
        Assert.DoesNotContain("new(0, 0, -", ledgerCodeBehind);
    }

    [Fact]
    public void Ledger_RowClickInteractiveScanStopsAtRowRootBeforeListViewAncestor()
    {
        var ledgerCodeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml.cs"));

        Assert.Contains("IsInteractiveLedgerRowElement(e.OriginalSource as DependencyObject, sender as DependencyObject)", ledgerCodeBehind);
        Assert.Contains("DependencyObject? rowRoot", ledgerCodeBehind);
        Assert.Contains("ReferenceEquals(source, rowRoot)", ledgerCodeBehind);
    }

    [Fact]
    public void Ledger_GroupItemsAnimateAndRowsDoNotRenderSeparatorForLastItem()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var groupStyleSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerGroupItemStyle\"", "x:Key=\"LedgerGroupStyle\"");
        var rowTemplateSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerRowTemplate\"", "x:Key=\"LedgerGroupItemStyle\"");

        Assert.Contains("LedgerGroupItemsClip", groupStyleSection);
        Assert.Contains("DoubleAnimation", groupStyleSection);
        Assert.Contains("Storyboard.TargetProperty=\"Opacity\"", groupStyleSection);
        Assert.Contains("Storyboard.TargetProperty=\"MaxHeight\"", groupStyleSection);
        Assert.DoesNotContain("x:Name=\"RowSeparator\"", rowTemplateSection);
        Assert.DoesNotContain("BorderThickness=\"0,0,0,1\"", rowTemplateSection);
    }

    [Fact]
    public void Ledger_EditTagPopupUsesEditableTags()
    {
        var ledgerXaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));
        var ledgerCodeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml.cs"));
        var rowTemplateSection = ExtractSection(ledgerXaml, "x:Key=\"LedgerRowTemplate\"", "x:Key=\"LedgerGroupItemStyle\"");

        Assert.Contains("x:Name=\"LedgerEditTagPopup\"", rowTemplateSection);
        Assert.Contains("ItemsSource=\"{Binding DataContext.EditableTags, ElementName=LedgerRoot}\"", rowTemplateSection);
        Assert.Contains("SelectionChanged=\"OnEditTagSelectionChanged\"", rowTemplateSection);
        Assert.Contains("OnTransactionTagPreviewMouseLeftButtonDown", ledgerCodeBehind);
        Assert.Contains("ApplyTransactionTag", ledgerCodeBehind);
    }

    [Fact]
    public void MainWindow_ReceivesLedgerNavigationRequestsAndCleansUpRegistration()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs"));

        Assert.Contains("Register<MainWindow, NavigateToLedgerRequestedMessage>", source);
        Assert.Contains("NavigateToMainPageAsync(MainPage.Ledger)", source);
        Assert.Contains("Unregister<NavigateToLedgerRequestedMessage>(this)", source);
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

    private static string ExtractSection(string source, string startMarker, string endMarker)
    {
        var startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Start marker '{startMarker}' was not found.");
        var endIndex = source.IndexOf(endMarker, startIndex + startMarker.Length, StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"End marker '{endMarker}' was not found after '{startMarker}'.");
        return source.Substring(startIndex, endIndex - startIndex);
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
