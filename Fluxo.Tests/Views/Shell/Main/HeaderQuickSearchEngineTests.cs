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
        var result = HeaderQuickSearchEngine.Search(
            [CreateExpenseLog(1, "Groceries", DateTime.Today)],
            [CreateIncomeLog(2, "Salary", DateTime.Today)],
            query);

        Assert.Empty(result);
    }

    [Fact]
    public void Search_FiltersExpensesAndIncomes_ByNameUsingCaseInsensitiveContains()
    {
        var expenses = new[]
        {
            CreateExpenseLog(1, "Monthly Grocery Run", DateTime.Today.AddDays(-1)),
            CreateExpenseLog(2, "Electric Bill", DateTime.Today)
        };
        var incomes = new[]
        {
            CreateIncomeLog(3, "gRoCeRy Reimbursement", DateTime.Today),
            CreateIncomeLog(4, "Salary", DateTime.Today)
        };

        var result = HeaderQuickSearchEngine.Search(expenses, incomes, "gRoCeR").ToList();

        Assert.Equal([3, 1], result.Select(item => item.Id).ToArray());
        Assert.Equal(
            [HeaderQuickSearchResultKind.Income, HeaderQuickSearchResultKind.Expense],
            result.Select(item => item.Kind).ToArray());
    }

    [Fact]
    public void Search_UsesTrimmedQuery_WhenEffectiveLengthIsFour()
    {
        var expenses = new[]
        {
            CreateExpenseLog(1, "Apartment rent", DateTime.Today.AddDays(-2))
        };
        var incomes = new[]
        {
            CreateIncomeLog(2, "Rent refund", DateTime.Today)
        };

        var result = HeaderQuickSearchEngine.Search(expenses, incomes, "  rent  ").ToList();

        Assert.Equal([2, 1], result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void Search_LimitsResultsToFiveTotalMatches()
    {
        var expenses = Enumerable
            .Range(1, 4)
            .Select(index => CreateExpenseLog(index, $"Coffee expense {index}", DateTime.Today.AddDays(-index)))
            .ToList();
        var incomes = Enumerable
            .Range(5, 4)
            .Select(index => CreateIncomeLog(index, $"Coffee income {index}", DateTime.Today.AddDays(-index)))
            .ToList();

        var result = HeaderQuickSearchEngine.Search(expenses, incomes, "coffee").ToList();

        Assert.Equal(5, result.Count);
        Assert.Equal([1, 2, 3, 4, 5], result.Select(item => item.Id).ToArray());
    }

    [Fact]
    public void Search_IgnoresNullNestedFields_WithoutThrowing()
    {
        var expenses = new[]
        {
            new ExpenseLogVM { Id = 1, Expense = null! },
            new ExpenseLogVM { Id = 2, Expense = new ExpenseVM { Name = null! } },
            CreateExpenseLog(3, "Rent payment", DateTime.Today.AddDays(-1))
        };
        var incomes = new[]
        {
            new IncomeLogVM { Id = 4, Name = null! },
            CreateIncomeLog(5, "Rent refund", DateTime.Today)
        };

        var exception = Record.Exception(() => HeaderQuickSearchEngine.Search(expenses, incomes, "rent").ToList());

        Assert.Null(exception);
        var result = HeaderQuickSearchEngine.Search(expenses, incomes, "rent").ToList();
        Assert.Equal([5, 3], result.Select(item => item.Id).ToArray());
    }

    private static ExpenseLogVM CreateExpenseLog(int id, string expenseName, DateTime deductedOn)
    {
        return new ExpenseLogVM
        {
            Id = id,
            Amount = id * 10m,
            DeductedOn = deductedOn,
            Expense = new ExpenseVM
            {
                Name = expenseName,
                Tag = new TagVM
                {
                    Id = 10 + id,
                    Name = "Tag",
                    HexCode = "#FFAA00"
                }
            },
            Account = new AccountVM { Id = 20 + id, Name = "Wallet" }
        };
    }

    private static IncomeLogVM CreateIncomeLog(int id, string incomeName, DateTime addedOn)
    {
        return new IncomeLogVM
        {
            Id = id,
            Name = incomeName,
            Amount = id * 100m,
            AddedOn = addedOn,
            Account = new AccountVM { Id = 30 + id, Name = "Checking" }
        };
    }
}
