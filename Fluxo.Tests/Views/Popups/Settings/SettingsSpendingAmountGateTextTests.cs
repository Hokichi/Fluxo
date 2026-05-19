using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsSpendingAmountGateTextTests
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void FixedExpensesEmptyStateText_IsBoundToButtonFromViewModel()
    {
        var xaml = ReadXaml("SettingsFixedExpensesTab.xaml");
        Assert.Contains("Content=\"{Binding FixedExpensesEmptyStateText}\"", xaml);
        Assert.Contains("Click=\"OnSpendingAmountGateActionClick\"", xaml);
    }

    [Fact]
    public void GoalsEmptyStateText_IsBoundToButtonFromViewModel()
    {
        var xaml = ReadXaml("SettingsGoalsTab.xaml");
        Assert.Contains("Content=\"{Binding GoalsEmptyStateText}\"", xaml);
        Assert.Contains("Click=\"OnSpendingAmountGateActionClick\"", xaml);
    }

    [Fact]
    public void BudgetBlockedStateText_IsBoundToButtonFromViewModel()
    {
        var xaml = ReadXaml("SettingsBudgetTab.xaml");
        Assert.Contains("Content=\"{Binding BudgetBlockedStateText}\"", xaml);
        Assert.Contains("Click=\"OnSpendingAmountGateActionClick\"", xaml);
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
