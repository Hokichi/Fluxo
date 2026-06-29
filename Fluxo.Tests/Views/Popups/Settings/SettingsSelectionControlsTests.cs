using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsSelectionControlsTests
{
    [Theory]
    [InlineData("SettingsAccountsTab.xaml", "IsAccountChecksEnabled", "HasCheckedAccounts")]
    [InlineData("SettingsRecurringTransactionsTab.xaml", "IsRecurringTransactionChecksEnabled", "HasCheckedRecurringTransactions")]
    [InlineData("SettingsGoalsTab.xaml", "IsGoalChecksEnabled", "HasCheckedGoals")]
    public void SelectableTab_UsesCombinedControlsAndDisablesEmptyBulkActions(
        string fileName,
        string checksProperty,
        string checkedItemsProperty)
    {
        var xaml = ReadTab(fileName);

        Assert.Contains("<customControls:BalloonCheckBox", xaml);
        Assert.Contains($"IsChecked=\"{{Binding {checksProperty}, Mode=OneWay}}\"", xaml);
        Assert.Contains("UncheckedIcon=\"{StaticResource CheckAll}\"", xaml);
        Assert.Contains("CheckedIcon=\"{StaticResource UncheckAll}\"", xaml);
        Assert.Contains($"IsEnabled=\"{{Binding {checkedItemsProperty}}}\"", xaml);
        Assert.Contains($"DataContext.{checksProperty}", xaml);
        Assert.Contains("TargetName=\"RowActions\" Property=\"Visibility\" Value=\"Collapsed\"", xaml);
    }

    [Fact]
    public void AccountsTab_UsesCombinedPinAndEnableControls()
    {
        var xaml = ReadTab("SettingsAccountsTab.xaml");

        Assert.Contains("Tag=\"Accounts:PinToggle\"", xaml);
        Assert.Contains("Tag=\"Accounts:EnableToggle\"", xaml);
        Assert.Contains("UncheckedText=\"Pin Selected\"", xaml);
        Assert.Contains("CheckedText=\"Unpin Selected\"", xaml);
    }

    [Theory]
    [InlineData("SettingsRecurringTransactionsTab.xaml", "RecurringTransactions:EnableToggle")]
    [InlineData("SettingsGoalsTab.xaml", "Goals:EnableToggle")]
    public void EnableableTab_UsesCombinedEnableControl(string fileName, string tag)
    {
        var xaml = ReadTab(fileName);

        Assert.Contains($"Tag=\"{tag}\"", xaml);
        Assert.Contains("UncheckedText=\"Enable Selected\"", xaml);
        Assert.Contains("CheckedText=\"Disable Selected\"", xaml);
    }

    [Theory]
    [InlineData("SettingsAccountsTab.xaml.cs", "IsAccountChecksEnabled")]
    [InlineData("SettingsRecurringTransactionsTab.xaml.cs", "IsRecurringTransactionChecksEnabled")]
    [InlineData("SettingsGoalsTab.xaml.cs", "IsGoalChecksEnabled")]
    public void SelectableTab_DoubleClickGuardChecksSelectionMode(string fileName, string propertyName)
    {
        var source = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "Tabs", fileName));
        Assert.Contains($"if (e.ClickCount < 2 || _viewModel.{propertyName})", source);
    }

    private static string ReadTab(string fileName) =>
        File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "Tabs", fileName));
}
