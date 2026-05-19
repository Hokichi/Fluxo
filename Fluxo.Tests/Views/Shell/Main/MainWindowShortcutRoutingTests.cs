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
    public void SpendingAmountGateHelper_IsDeclared()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("private bool IsDashboardSpendingAmountGateLocked()", source);
        Assert.Contains("return _mainVM.IsDashboardSpendingAmountGateLocked;", source);
    }

    [Fact]
    public void LockedShortcuts_AreSuppressed()
    {
        var source = ReadMainWindowSource();

        Assert.Contains("if (IsDashboardSpendingAmountGateLocked())", source);
        Assert.Contains("e.Handled = true;", source);
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenSearchShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenAnalyticsShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Control)", source);
        Assert.Contains("if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z && !IsTextInputElementFocused()", source);
        Assert.Contains("if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Y && !IsTextInputElementFocused()", source);
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
        Assert.Contains("if (IsDashboardSpendingAmountGateLocked())", source);
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
        Assert.Contains("if (IsDashboardSpendingAmountGateLocked())", source);
    }

    private static string ReadMainWindowSource()
    {
        var filePath = Path.Combine(GetRepositoryRootPath(), "Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs");
        return File.ReadAllText(filePath);
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
