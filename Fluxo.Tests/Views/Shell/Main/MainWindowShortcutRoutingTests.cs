using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowShortcutRoutingTests
{
    [Fact]
    public void CtrlNShortcut_RoutesToAddNewTransactionPopup()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenNewTransactionShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenAddNewTransactionPopup();", source);
    }

    [Fact]
    public void CtrlQShortcut_RoutesToQuickAccessPopup()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenQuickAccessShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenQuickAddPopup();", source);
    }

    [Fact]
    public void CtrlSlashShortcut_RoutesToHotkeysOverviewPopup()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenHotkeysOverviewShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenHotkeysOverviewPopup();", source);
    }

    [Fact]
    public void HeaderMenu_IncludesHotkeysItem()
    {
        var source = ReadMainWindowSource();
        var xaml = ReadMainWindowXaml();

        Assert.Contains("Click=\"OnHotkeysButtonClick\"", xaml);
        Assert.Contains("Path=\"{StaticResource KeyboardBoxFill}\"", xaml);
        Assert.Contains("Text=\"Hotkeys\"", xaml);
        Assert.Contains("Text=\"Ctrl+/\"", xaml);
        Assert.Contains("private void OnHotkeysButtonClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("OpenHotkeysOverviewPopup();", source);
    }

    [Fact]
    public void CtrlShiftNShortcut_RoutesToRecurringAddNewTransactionPopup()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenRecurringNewTransactionShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenRecurringAddNewTransactionPopup();", source);
        Assert.Contains("popupViewModel.InitializeRecurringMode(isLocked: false);", source);
    }

    [Fact]
    public void AnalyticsShortcut_NavigatesToAnalyticsMainPage()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenAnalyticsShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("await NavigateToMainPageAsync(MainPage.Analytics);", source);
    }

    [Fact]
    public void MainPageShortcuts_NavigateToRequestedPage()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenDashboardShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("await NavigateToMainPageAsync(MainPage.Dashboard);", source);
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenCalendarShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("await NavigateToMainPageAsync(MainPage.Calendar);", source);
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenLedgerShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("await NavigateToMainPageAsync(MainPage.Ledger);", source);
    }

    [Fact]
    public void SettingsShortcut_UsesCtrlCommaOnly()
    {
        var source = ReadMainWindowSource();
        var xaml = ReadMainWindowXaml();

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenSettingsShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenSettingsPopup();", source);
        Assert.Contains("Text=\"Ctrl+,\"", xaml);
        Assert.DoesNotContain("Text=\"Ctrl+S\"", xaml);
        Assert.DoesNotContain("e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control", source);
    }

    [Fact]
    public void RemovedSetupWizardShortcut_IsNotRouted()
    {
        var source = ReadMainWindowSource();

        Assert.DoesNotContain("IsRunSetupWizardShortcut", source);
        Assert.DoesNotContain("RunSetupWizardFromShortcutAsync", source);
    }

    [Fact]
    public void PlanningShortcut_RemainsRoutedAfterMenuRemoval()
    {
        var source = ReadMainWindowSource();
        var xaml = ReadMainWindowXaml();

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenPlanningShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenPlanningPopup();", source);
        Assert.DoesNotContain("x:Name=\"PlanningMenuButton\"", xaml);
        Assert.DoesNotContain("Click=\"OnPlanningButtonClick\"", xaml);
    }

    [Fact]
    public void SpendingAmountGateHelper_IsDeclared()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("private bool IsDashboardSpendingAmountGateLocked()", source);
        Assert.Contains("return _mainVM.IsDashboardSpendingAmountGateLocked;", source);
        Assert.Contains("private bool IsSufficientFundsActionGateLocked()", source);
        Assert.Contains("return _mainVM.IsSufficientFundsActionGateLocked;", source);
    }

    [Fact]
    public void LockedShortcuts_AreSuppressed()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (IsSufficientFundsActionGateLocked())", source);
        Assert.Contains("e.Handled = true;", source);
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenNewTransactionShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenRecurringNewTransactionShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenSearchShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenAnalyticsShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z && !IsTextInputElementFocused()", source);
        Assert.Contains("if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y && !IsTextInputElementFocused()", source);
    }

    [Fact]
    public void LockedShortcuts_StillAllowSources()
    {
        var source = ReadMainWindowSource();

        var sourcesBranch = Slice(
            source,
            "if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)",
            "if (MainWindowShortcutMatcher.IsOpenAddAccountShortcut(e.Key, Keyboard.Modifiers))");

        Assert.DoesNotContain("IsDashboardSpendingAmountGateLocked()", sourcesBranch);
        Assert.DoesNotContain("IsSufficientFundsActionGateLocked()", sourcesBranch);
        Assert.Contains("OpenAccountsListPopup();", sourcesBranch);
    }

    [Fact]
    public void LockedShortcuts_SuppressNewTransaction()
    {
        var source = ReadMainWindowSource();

        var branch = Slice(
            source,
            "if (MainWindowShortcutMatcher.IsOpenNewTransactionShortcut(e.Key, Keyboard.Modifiers))",
            "if (MainWindowShortcutMatcher.IsOpenRecurringNewTransactionShortcut(e.Key, Keyboard.Modifiers))");

        Assert.Contains("IsSufficientFundsActionGateLocked()", branch);
        Assert.Contains("OpenAddNewTransactionPopup();", branch);
    }

    [Fact]
    public void LockedShortcuts_SuppressRecurringNewTransaction()
    {
        var source = ReadMainWindowSource();

        var branch = Slice(
            source,
            "if (MainWindowShortcutMatcher.IsOpenRecurringNewTransactionShortcut(e.Key, Keyboard.Modifiers))",
            "if (MainWindowShortcutMatcher.IsOpenSearchShortcut(e.Key, Keyboard.Modifiers))");

        Assert.Contains("IsSufficientFundsActionGateLocked()", branch);
        Assert.Contains("OpenRecurringAddNewTransactionPopup();", branch);
    }

    [Fact]
    public void AdditionalPopupShortcuts_RouteToTargetPopups()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenAddAccountShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenAddAccountPopup();", source);
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenAddSavingGoalShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenAddSavingGoalPopup();", source);
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenDataManagementShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenDataManagementPopup();", source);
    }

    [Fact]
    public void NotificationShortcut_TogglesHeaderNotificationPopup()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (MainWindowShortcutMatcher.IsToggleNotificationsShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("ToggleHeaderNotificationPopup();", source);
    }

    [Fact]
    public void EscapeKey_ClosesHeaderNotificationPopupBeforeGlobalShortcuts()
    {
        var source = ReadMainWindowSource();
        var keyDownMethod = Slice(
            source,
            "private async void OnPreviewKeyDown(object sender, KeyEventArgs e)",
            "private static bool IsTextInputElementFocused()");

        Assert.Contains("if (HeaderNotificationPopup.IsOpen && e.Key == Key.Escape)", keyDownMethod);
        Assert.Contains("CloseHeaderNotificationPopup();", keyDownMethod);
        Assert.True(
            keyDownMethod.IndexOf("if (HeaderNotificationPopup.IsOpen && e.Key == Key.Escape)", StringComparison.Ordinal) <
            keyDownMethod.IndexOf("if (MainWindowShortcutMatcher.IsOpenNewTransactionShortcut", StringComparison.Ordinal));
    }

    [Fact]
    public void DashboardPeriodShortcuts_AreDashboardOnlyAndCurrentShortcutNoOpsWhenCurrent()
    {
        var source = ReadMainWindowSource();
        var method = Slice(
            source,
            "private async Task<bool> TryHandleDashboardPeriodShortcut",
            "private async Task<bool> TryHandleLedgerPeriodShortcut");

        Assert.Contains("TryHandleDashboardPeriodShortcut(e.Key, Keyboard.Modifiers)", source);
        Assert.Contains("if (_activeMainPage != MainPage.Dashboard)", method);
        Assert.Contains("await _mainVM.DaySpinner.SelectAdjacentVisibleDayFromUserAsync(-1, this);", method);
        Assert.Contains("await _mainVM.DaySpinner.SelectAdjacentVisibleDayFromUserAsync(1, this);", method);
        Assert.Contains("if (_mainVM.Dashboard.ViewModeToggle.IsAtCurrentPeriod)", method);
        Assert.Contains("await _mainVM.Dashboard.ViewModeToggle.MoveToCurrentPeriodFromUserAsync(this);", method);
        Assert.Contains("IsNavigateDashboardPreviousPeriodShortcut(key, modifiers)", method);
        Assert.Contains("IsNavigateDashboardNextPeriodShortcut(key, modifiers)", method);
    }

    [Fact]
    public void LedgerCurrentPeriodShortcut_IsLedgerOnlyAndAppliesCurrentRange()
    {
        var source = ReadMainWindowSource();
        var keyDownMethod = Slice(
            source,
            "private async void OnPreviewKeyDown(object sender, KeyEventArgs e)",
            "private async Task<bool> TryHandleDashboardPeriodShortcut");
        var method = Slice(
            source,
            "private async Task<bool> TryHandleLedgerPeriodShortcut",
            "private async Task<bool> TryHandleViewModeShortcut");

        Assert.Contains("TryHandleLedgerPeriodShortcut(e.Key, Keyboard.Modifiers)", keyDownMethod);
        Assert.Contains("if (_activeMainPage != MainPage.Ledger || _mainVM.Ledger is null)", method);
        Assert.Contains("IsNavigateDashboardCurrentPeriodShortcut(key, modifiers)", method);
        Assert.Contains("if (_mainVM.Ledger.ViewModeToggle.IsAtCurrentPeriod)", method);
        Assert.Contains("await _mainVM.Ledger.ViewModeToggle.MoveToCurrentPeriodFromUserAsync(this);", method);
        Assert.Contains("ApplyMainWindowRangeToLedger();", method);
    }

    [Fact]
    public void ViewModeShortcuts_TargetDashboardAndLedger()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("TryHandleViewModeShortcut(e.Key, Keyboard.Modifiers)", source);
        Assert.Contains("_mainVM.Dashboard.ViewModeToggle", source);
        Assert.Contains("_mainVM.Ledger?.ViewModeToggle", source);
        Assert.Contains("SetSelectedMainContentViewFromUserAsync(viewMode, this)", source);
    }

    [Fact]
    public void LedgerShortcuts_AreLedgerOnly()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("TryHandleLedgerShortcut(e.Key, Keyboard.Modifiers)", source);
        Assert.Contains("if (_activeMainPage != MainPage.Ledger || _ledgerPageView is null)", source);
        Assert.Contains("_ledgerPageView.ExportDataFromShortcutAsync();", source);
        Assert.Contains("_ledgerPageView.ClearFiltersFromShortcutAsync();", source);
        Assert.Contains("_ledgerPageView.ApplyAmountSortDirectionFromShortcutAsync(LedgerAmountSortDirection.Ascending);", source);
        Assert.Contains("_ledgerPageView.ApplyAmountSortDirectionFromShortcutAsync(LedgerAmountSortDirection.Descending);", source);
    }

    [Fact]
    public void SourcePopupOpenMethods_AreNotGuardedBySufficientFundsGate()
    {
        var source = ReadMainWindowSource();

        var sourcesMethod = Slice(
            source,
            "public void OpenAccountsListPopup()",
            "public void OpenAddAccountPopup()");
        var addSourceMethod = Slice(
            source,
            "public void OpenAddAccountPopup()",
            "public void OpenAddSavingGoalPopup()");

        Assert.DoesNotContain("IsDashboardSpendingAmountGateLocked()", sourcesMethod);
        Assert.DoesNotContain("IsSufficientFundsActionGateLocked()", sourcesMethod);
        Assert.DoesNotContain("IsDashboardSpendingAmountGateLocked()", addSourceMethod);
        Assert.DoesNotContain("IsSufficientFundsActionGateLocked()", addSourceMethod);
    }

    [Fact]
    public void ClickHandlers_AreGuardedBySpendingAmountGate()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("private void OnHeaderSearchButtonClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("private void OnHeaderQuickAddButtonClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("private void OnQuickAddButtonClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("OpenQuickAddPopup();", Slice(
            source,
            "private void OnQuickAddButtonClick(object sender, RoutedEventArgs e)",
            "private void OnHeaderMenuButtonMouseEnter(object sender, MouseEventArgs e)"));
        Assert.Contains("private void OnAccountsButtonClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("private async void OnAnalyticsNavigationClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("private async Task NavigateToMainPageAsync(MainPage page)", source);
        Assert.Contains("if (IsSufficientFundsActionGateLocked())", source);
    }

    [Fact]
    public void LedgerSearchClickAway_PreservesNonEmptySearch()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("!IsDescendantOf(source, HeaderSearchRegion) &&", source);
        Assert.Contains("ShouldCollapseHeaderSearchOnExternalClick())", source);
        Assert.Contains("private bool ShouldCollapseHeaderSearchOnExternalClick()", source);
        Assert.Contains(
            "_activeMainPage != MainPage.Ledger || string.IsNullOrWhiteSpace(HeaderSearchBox.Text)",
            source);
    }

    [Fact]
    public void HeaderSearchExpandCollapse_UsesAnimatedWidthAndOpacity()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("private const double HeaderSearchCollapsedWidth", source);
        Assert.Contains("private const double HeaderSearchExpandedWidth = 160", source);
        Assert.Contains("private void AnimateHeaderSearchInput(double targetWidth, double targetOpacity", source);
        Assert.Contains("new DoubleAnimation(currentWidth, targetWidth", source);
        Assert.Contains("HeaderSearchInputBorder.BeginAnimation(FrameworkElement.WidthProperty", source);
        Assert.Contains("HeaderSearchInputBorder.BeginAnimation(UIElement.OpacityProperty", source);
    }

    [Fact]
    public void HeaderSearchCollapse_ClearsTextAndResultsBeforeShrinkAnimation()
    {
        var source = ReadMainWindowSource();
        var collapseMethod = Slice(
            source,
            "private void CollapseHeaderSearch()",
            "private void UpdateHeaderSearchResults()");

        Assert.True(
            collapseMethod.IndexOf("HeaderSearchResultsPopup.IsOpen = false;", StringComparison.Ordinal) <
            collapseMethod.IndexOf("AnimateHeaderSearchInput(HeaderSearchCollapsedWidth", StringComparison.Ordinal));
        Assert.True(
            collapseMethod.IndexOf("HeaderSearchBox.Text = string.Empty;", StringComparison.Ordinal) <
            collapseMethod.IndexOf("AnimateHeaderSearchInput(HeaderSearchCollapsedWidth", StringComparison.Ordinal));
        Assert.True(
            collapseMethod.IndexOf("_headerSearchResults.Clear();", StringComparison.Ordinal) <
            collapseMethod.IndexOf("AnimateHeaderSearchInput(HeaderSearchCollapsedWidth", StringComparison.Ordinal));
        Assert.True(
            collapseMethod.IndexOf("AnimateHeaderSearchInput(HeaderSearchCollapsedWidth", StringComparison.Ordinal) <
            collapseMethod.IndexOf("HeaderSearchInputBorder.Visibility = Visibility.Collapsed;", StringComparison.Ordinal));
    }

    [Fact]
    public void PopupOpenMethods_AreGuardedBySpendingAmountGate()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("public void OpenQuickAddPopup(AddNewTransactionVM.AddNewTransactionDraft? draft = null)", source);
        Assert.Contains("public void OpenAddNewTransactionPopup(AddNewTransactionVM.AddNewTransactionDraft? draft = null)", source);
        Assert.Contains("public void OpenRecurringAddNewTransactionPopup()", source);
        Assert.Contains("public void OpenAccountsListPopup()", source);
        Assert.Contains("public void OpenAddAccountPopup()", source);
        Assert.Contains("public void OpenAnalyticsPopup()", source);
        Assert.Contains("private async Task OpenAnalyticsPopupAsync()", source);
        Assert.Contains("if (IsSufficientFundsActionGateLocked())", source);
    }

    private static string ReadMainWindowSource()
    {
        var filePath = Path.Combine(GetRepositoryRootPath(), "Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs");
        return File.ReadAllText(filePath);
    }

    private static string ReadMainWindowXaml()
    {
        var filePath = Path.Combine(GetRepositoryRootPath(), "Fluxo", "Views", "Shell", "Main", "MainWindow.xaml");
        return File.ReadAllText(filePath);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");

        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find end marker '{endMarker}'.");

        return source[start..end];
    }

    private static string GetRepositoryRootPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
                return currentDirectory.FullName;

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }
}
