using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class BudgetAllocationPanelRecentActivitiesTests
{
    [Fact]
    public void TransactionsList_UsesRestoredAndMaximizedHardCaps()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo", "Views", "Shell", "Main", "Sections", "BudgetAllocationPanel.xaml"));

        Assert.Contains("<Setter Property=\"MaxVisibleItems\" Value=\"5\" />", xaml);
        Assert.Contains("IsWindowLayoutMaximized", xaml);
        Assert.Contains("<Setter Property=\"MaxVisibleItems\" Value=\"8\" />", xaml);
        Assert.DoesNotContain("HasMoreItems=\"{Binding TransactionsHasMoreItems}\"", xaml);
        Assert.DoesNotContain("IsLoading=\"{Binding IsTransactionsLoading}\"", xaml);
        Assert.DoesNotContain("LoadMoreCommand=\"{Binding LoadMoreTransactionsCommand}\"", xaml);
    }
}
