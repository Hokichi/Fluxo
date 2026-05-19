using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsSpendingAmountGateTextTests
{
    private static readonly XNamespace PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void FixedExpensesEmptyStateText_IsBoundFromViewModel()
    {
        var xaml = ReadXaml("SettingsFixedExpensesTab.xaml");
        Assert.Contains("Text=\"{Binding FixedExpensesEmptyStateText}\"", xaml);
    }

    [Fact]
    public void GoalsEmptyStateText_IsBoundFromViewModel()
    {
        var xaml = ReadXaml("SettingsGoalsTab.xaml");
        Assert.Contains("Text=\"{Binding GoalsEmptyStateText}\"", xaml);
    }

    [Fact]
    public void BudgetBlockedStateText_IsBoundFromViewModel()
    {
        var xaml = ReadXaml("SettingsBudgetTab.xaml");
        Assert.Contains("Text=\"{Binding BudgetBlockedStateText}\"", xaml);
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
