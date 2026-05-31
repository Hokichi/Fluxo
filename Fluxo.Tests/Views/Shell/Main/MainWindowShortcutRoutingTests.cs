using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowShortcutRoutingTests
{
    [Fact]
    public void CtrlNShortcut_RoutesToQuickAddPopup()
    {
        var filePath = Path.Combine(GetRepositoryRootPath(), "Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs");
        var source = File.ReadAllText(filePath);

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenQuickAddShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenQuickAddPopup();", source);
    }

    [Fact]
    public void EscapeShortcut_ClosesAnalyticsDrawerWhenOpen()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (_isAnalyticsDrawerOpen && MainWindowShortcutMatcher.IsCloseAnalyticsShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("CloseAnalyticsDrawer();", source);
        Assert.Contains("e.Handled = true;", source);
    }

    [Fact]
    public void AnalyticsDrawer_TakesKeyboardFocusAfterOpening()
    {
        var source = ReadMainWindowSource();
        var xaml = ReadMainWindowXaml();

        Assert.Contains("x:Name=\"AnalyticsDrawerPanel\"", xaml);
        Assert.Contains("Focusable=\"True\"", xaml);
        Assert.Contains("private void FocusAnalyticsDrawerForShortcuts()", source);
        Assert.Contains("AnalyticsDrawerPanel.Focus();", source);
        Assert.Contains("Keyboard.Focus(AnalyticsDrawerPanel);", source);
        Assert.Contains("FocusAnalyticsDrawerForShortcuts();", source);
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
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenQuickAddShortcut(e.Key, Keyboard.Modifiers))", source);
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
            "if (MainWindowShortcutMatcher.IsRunSetupWizardShortcut(e.Key, Keyboard.Modifiers))");

        Assert.DoesNotContain("IsDashboardSpendingAmountGateLocked()", sourcesBranch);
        Assert.DoesNotContain("IsSufficientFundsActionGateLocked()", sourcesBranch);
        Assert.Contains("OpenSpendingSourcesListPopup();", sourcesBranch);
    }

    [Fact]
    public void LockedShortcuts_SuppressQuickAdd()
    {
        var source = ReadMainWindowSource();

        var quickAddBranch = Slice(
            source,
            "if (MainWindowShortcutMatcher.IsOpenQuickAddShortcut(e.Key, Keyboard.Modifiers))",
            "if (MainWindowShortcutMatcher.IsOpenSearchShortcut(e.Key, Keyboard.Modifiers))");

        Assert.Contains("IsSufficientFundsActionGateLocked()", quickAddBranch);
        Assert.Contains("OpenQuickAddPopup();", quickAddBranch);
    }

    [Fact]
    public void SourcePopupOpenMethods_AreNotGuardedBySufficientFundsGate()
    {
        var source = ReadMainWindowSource();

        var sourcesMethod = Slice(
            source,
            "public void OpenSpendingSourcesListPopup()",
            "public void OpenAddSpendingSourcePopup()");
        var addSourceMethod = Slice(
            source,
            "public void OpenAddSpendingSourcePopup()",
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
        Assert.Contains("private void OnSpendingSourcesButtonClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("private void OnAddSpendingSourceButtonClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("private async void OnAnalyticsDrawerTabClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("if (IsSufficientFundsActionGateLocked())", source);
    }

    [Fact]
    public void PopupOpenMethods_AreGuardedBySpendingAmountGate()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("public void OpenQuickAddPopup(QuickAddVM.QuickAddDraft? draft = null)", source);
        Assert.Contains("public void OpenAddNewTransactionPopup(QuickAddVM.QuickAddDraft? draft = null)", source);
        Assert.Contains("public void OpenSpendingSourcesListPopup()", source);
        Assert.Contains("public void OpenAddSpendingSourcePopup()", source);
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
