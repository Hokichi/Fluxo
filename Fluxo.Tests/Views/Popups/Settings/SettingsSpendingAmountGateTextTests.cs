using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsSpendingAmountGateTextTests
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void FixedExpensesEmptyState_DoesNotShowSpendingAmountGateAction()
    {
        var xaml = ReadXaml("SettingsFixedExpensesTab.xaml");
        Assert.DoesNotContain("Add a spending amount to start using fluxo", xaml);
        Assert.DoesNotContain("Content=\"{Binding FixedExpensesEmptyStateText}\"", xaml);
    }

    [Fact]
    public void GoalsEmptyState_DoesNotShowSpendingAmountGateAction()
    {
        var xaml = ReadXaml("SettingsGoalsTab.xaml");
        Assert.DoesNotContain("Add a spending amount to start using fluxo", xaml);
        Assert.DoesNotContain("Content=\"{Binding GoalsEmptyStateText}\"", xaml);
    }

    [Fact]
    public void BudgetEmptyState_DoesNotShowSpendingAmountGateAction()
    {
        var xaml = ReadXaml("SettingsBudgetTab.xaml");
        Assert.DoesNotContain("Add a spending amount to start using fluxo", xaml);
        Assert.DoesNotContain("Content=\"{Binding BudgetBlockedStateText}\"", xaml);
    }

    [Fact]
    public void SettingsPopup_PutsAccountsBeforeBudgetManagement()
    {
        var document = XDocument.Parse(File.ReadAllText(GetRepositoryFilePath(
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "SettingsPopup.xaml")));

        var tabNames = document
            .Descendants(PresentationNamespace + "RadioButton")
            .Select(element => (string?)element.Attribute("Tag"))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!)
            .ToArray();

        Assert.Equal(
            [
                "Accounts",
                "Budget Management",
                "Recurring Transactions",
                "Goals",
                "Debt/IoUs",
                "Tags",
                "Preferences",
                "About"
            ],
            tabNames);
    }

    [Fact]
    public void SettingsPopup_HidesFooterSaveAndRevertButtons()
    {
        var xaml = File.ReadAllText(GetRepositoryFilePath(
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "SettingsPopup.xaml"));

        Assert.Contains("ShowSaveButton=\"False\"", xaml);
        Assert.Contains("ShowRevertButton=\"False\"", xaml);
        Assert.DoesNotContain("IsSaveButtonEnabled=\"{Binding HasPendingConfigurationChanges}\"", xaml);
        Assert.DoesNotContain("IsRevertButtonEnabled=\"{Binding HasPendingConfigurationChanges}\"", xaml);
    }

    [Fact]
    public void SettingsPopup_TabButtonsUseBudgetNavigationGuard()
    {
        var xaml = File.ReadAllText(GetRepositoryFilePath(
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "SettingsPopup.xaml"));

        var tabButtonCount = xaml.Split("<RadioButton", StringSplitOptions.None).Length - 1;
        var guardedTabButtonCount = xaml.Split(
            "PreviewMouseLeftButtonDown=\"OnSettingsTabPreviewMouseLeftButtonDown\"",
            StringSplitOptions.None).Length - 1;

        Assert.Equal(8, tabButtonCount);
        Assert.Equal(tabButtonCount, guardedTabButtonCount);
    }

    [Theory]
    [InlineData("BudgetTabButton")]
    [InlineData("FixedExpensesTabButton")]
    [InlineData("GoalsTabButton")]
    public void SettingsPopup_GatedTabsUseLockedTabStyle(string buttonName)
    {
        var xaml = File.ReadAllText(GetRepositoryFilePath(
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "SettingsPopup.xaml"));

        Assert.Contains("x:Key=\"LockedSettingsTabButtonStyle\"", xaml);
        Assert.Contains("Binding=\"{Binding IsSufficientFundsActionGateLocked}\"", xaml);
        Assert.Contains("Property=\"IsHitTestVisible\" Value=\"False\"", xaml);
        Assert.Contains("Property=\"Opacity\" Value=\"0.45\"", xaml);

        var expectedButton = $"x:Name=\"{buttonName}\"";
        var buttonIndex = xaml.IndexOf(expectedButton, StringComparison.Ordinal);
        Assert.True(buttonIndex >= 0, $"Could not find {buttonName}.");

        var nextButtonIndex = xaml.IndexOf("<RadioButton", buttonIndex + expectedButton.Length, StringComparison.Ordinal);
        var buttonMarkup = nextButtonIndex >= 0
            ? xaml[buttonIndex..nextButtonIndex]
            : xaml[buttonIndex..];

        Assert.Contains("Style=\"{StaticResource LockedSettingsTabButtonStyle}\"", buttonMarkup);
    }

    private static string ReadXaml(string fileName)
    {
        var xamlPath = Path.Combine(
            GetRepositoryRootPath(),
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "Tabs",
            fileName);

        var xaml = File.ReadAllText(xamlPath);
        _ = XDocument.Parse(xaml).Descendants(PresentationNamespace + "TextBlock").ToList();
        return xaml;
    }

    private static string GetRepositoryFilePath(params string[] relativeSegments)
    {
        return Path.Combine(GetRepositoryRootPath(), Path.Combine(relativeSegments));
    }

    private static string GetRepositoryRootPath([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startingPath in new[]
                 {
                     Path.GetDirectoryName(sourceFilePath),
                     Directory.GetCurrentDirectory(),
                     AppContext.BaseDirectory
                 })
        {
            if (string.IsNullOrWhiteSpace(startingPath))
                continue;

            var currentDirectory = new DirectoryInfo(startingPath);

            while (currentDirectory is not null)
            {
                var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
                var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
                if (File.Exists(solutionPath) || File.Exists(solutionXPath))
                    return currentDirectory.FullName;

                currentDirectory = currentDirectory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{Directory.GetCurrentDirectory()}' or '{AppContext.BaseDirectory}'.");
    }
}
