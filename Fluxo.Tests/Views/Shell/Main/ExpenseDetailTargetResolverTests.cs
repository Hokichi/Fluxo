using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;
using Fluxo.Views.Shell.Main;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class ExpenseDetailTargetResolverTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsRequestedExpense_WhenExpenseHasNoParent()
    {
        var appData = Substitute.For<IAppDataService>();
        var expense = new ExpenseLogVM { Id = 7 };

        var result = await ExpenseDetailTargetResolver.ResolveAsync(expense, appData);

        Assert.Same(expense, result);
        await appData.DidNotReceiveWithAnyArgs().GetExpenseLogByLogIdAsync(default);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsParentExpense_WhenExpenseHasParent()
    {
        var appData = Substitute.For<IAppDataService>();
        var child = new ExpenseLogVM { Id = 7, ParentLogId = 3 };
        appData.GetExpenseLogByLogIdAsync(3, Arg.Any<CancellationToken>())
            .Returns(new ExpenseLog
            {
                Id = 3,
                Amount = 100m,
                DeductedOn = new DateTime(2026, 6, 20),
                Notes = "parent note",
                AccountId = 2,
                Account = new Account
                {
                    Id = 2,
                    Name = "Checking",
                    AccountType = AccountType.Checking
                },
                ExpenseId = 11,
                Expense = new Expense
                {
                    Id = 11,
                    Name = "Parent expense",
                    Amount = 100m,
                    ExpenseCategory = ExpenseCategory.Needs,
                    ExpenseTagId = 5,
                    ExpenseTag = new ExpenseTag
                    {
                        Id = 5,
                        Name = "Groceries",
                        HexCode = "#22C55E"
                    }
                }
            });

        var result = await ExpenseDetailTargetResolver.ResolveAsync(child, appData);

        Assert.NotSame(child, result);
        Assert.Equal(3, result.Id);
        Assert.Null(result.ParentLogId);
        Assert.Equal("Parent expense", result.Expense.Name);
        Assert.Equal("Checking", result.Account.Name);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsRequestedExpense_WhenParentCannotBeLoaded()
    {
        var appData = Substitute.For<IAppDataService>();
        var child = new ExpenseLogVM { Id = 7, ParentLogId = 3 };
        appData.GetExpenseLogByLogIdAsync(3, Arg.Any<CancellationToken>())
            .Returns((ExpenseLog?)null);

        var result = await ExpenseDetailTargetResolver.ResolveAsync(child, appData);

        Assert.Same(child, result);
    }
}
