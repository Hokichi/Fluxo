using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class BudgetAllocationPanelEmptyActionTests
{
    [Fact]
    public void BudgetAllocationPanel_WiresEmptyActionsForEachAllocationList()
    {
        var xaml = File.ReadAllText(ResolveBudgetAllocationPanelXamlPath());

        Assert.Contains("EmptyActionText=\"Log a Need\"", xaml);
        Assert.Contains("EmptyActionParameter=\"{x:Static enums:ExpenseCategory.Needs}\"", xaml);
        Assert.Contains("EmptyActionText=\"Log a Want\"", xaml);
        Assert.Contains("EmptyActionParameter=\"{x:Static enums:ExpenseCategory.Wants}\"", xaml);
        Assert.Contains("EmptyActionText=\"Log an Investment/Saving\"", xaml);
        Assert.Contains("EmptyActionParameter=\"{x:Static enums:ExpenseCategory.Savings}\"", xaml);
        Assert.Equal(3, CountOccurrences(xaml, "EmptyActionClick=\"OnEmptyExpenseActionClick\""));
    }

    [Fact]
    public void BudgetAllocationPanel_HidesEmptyActionsWhenSufficientFundsActionGateIsLocked()
    {
        var xaml = File.ReadAllText(ResolveBudgetAllocationPanelXamlPath());

        Assert.Contains("x:Key=\"BudgetAllocationExpensesListStyle\"", xaml);
        Assert.Contains("DataContext.IsSufficientFundsActionGateLocked", xaml);
        Assert.Contains("Property=\"IsEmptyActionVisible\" Value=\"False\"", xaml);
        Assert.Equal(3, CountOccurrences(xaml, "Style=\"{StaticResource BudgetAllocationExpensesListStyle}\""));
    }

    [Fact]
    public void BudgetAllocationPanelCodeBehind_OpensCategoryPrefilledTransactionPopup()
    {
        var source = File.ReadAllText(ResolveBudgetAllocationPanelCodeBehindPath());

        Assert.Contains("private void OnEmptyExpenseActionClick(object sender, RoutedEventArgs e)", source);
        Assert.Contains("ExpenseCategory", source);
        Assert.Contains("OpenAddNewTransactionPopupForCategory", source);
    }

    [Fact]
    public void BudgetAllocationPanel_TagStripUsesStableFixedHeightLayout()
    {
        var xaml = File.ReadAllText(ResolveBudgetAllocationPanelXamlPath());

        Assert.Contains("x:Key=\"BudgetAllocationVisibleTagListViewItemStyle\"", xaml);
        Assert.Contains("<Setter Property=\"Margin\" Value=\"0\" />", xaml);
        Assert.Contains("Height=\"32\"", xaml);
        Assert.Contains("ItemContainerStyle=\"{StaticResource BudgetAllocationVisibleTagListViewItemStyle}\"", xaml);
        Assert.DoesNotContain(
            "ItemContainerStyle=\"{StaticResource ListViewItemStyle}\"\r\n                ItemTemplate=\"{StaticResource Tags}\"",
            xaml);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string ResolveBudgetAllocationPanelXamlPath() =>
        ResolveRepositoryFile("Fluxo", "Views", "Shell", "Main", "Sections", "BudgetAllocationPanel.xaml");

    private static string ResolveBudgetAllocationPanelCodeBehindPath() =>
        ResolveRepositoryFile("Fluxo", "Views", "Shell", "Main", "Sections", "BudgetAllocationPanel.xaml.cs");

    private static string ResolveRepositoryFile(params string[] relativeSegments)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            if (File.Exists(Path.Combine(currentDirectory.FullName, "Fluxo.slnx")))
                return Path.Combine(currentDirectory.FullName, Path.Combine(relativeSegments));

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing 'Fluxo.slnx'.");
    }
}
