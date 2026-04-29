using Fluxo.ViewModels.Entities;
using Fluxo.Views.Shell.Main;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public class HeaderQuickSearchEngineTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("  abc  ")]
    public void Search_ReturnsEmpty_WhenQueryIsNullEmptyOrTooShort(string? query)
    {
        var logs = new[]
        {
            CreateLog(1, "Groceries"),
            CreateLog(2, "Transport")
        };

        var result = HeaderQuickSearchEngine.Search(logs, query);

        Assert.Empty(result);
    }

    [Fact]
    public void Search_FiltersByExpenseName_UsingCaseInsensitiveContains()
    {
        var logs = new[]
        {
            CreateLog(1, "Monthly Grocery Run"),
            CreateLog(2, "Electric Bill"),
            CreateLog(3, "gRoCeRy Delivery")
        };

        var result = HeaderQuickSearchEngine.Search(logs, "gRoCeR").ToList();

        Assert.Equal([1, 3], result.Select(log => log.Id).ToArray());
    }

    [Fact]
    public void Search_LimitsResultsToFirstFiveMatches()
    {
        var logs = Enumerable
            .Range(1, 7)
            .Select(index => CreateLog(index, $"Coffee expense {index}"))
            .ToList();

        var result = HeaderQuickSearchEngine.Search(logs, "coffee").ToList();

        Assert.Equal(5, result.Count);
        Assert.Equal([1, 2, 3, 4, 5], result.Select(log => log.Id).ToArray());
    }

    private static ExpenseLogVM CreateLog(int id, string expenseName)
    {
        return new ExpenseLogVM
        {
            Id = id,
            Expense = new ExpenseVM
            {
                Name = expenseName
            }
        };
    }
}
