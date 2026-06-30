using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;
using Fluxo.Views.Shell.Main;
using Fluxo.Helper.MainWindow;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class TransactionDetailTargetResolverTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsTransaction_WhenItHasNoParent()
    {
        var appData = Substitute.For<IAppDataService>();
        var transaction = new TransactionVM { Id = 7 };

        Assert.Same(transaction, await TransactionDetailTargetResolver.ResolveAsync(transaction, appData));
        await appData.DidNotReceiveWithAnyArgs().GetTransactionByIdAsync(default, default);
    }

    [Theory]
    [InlineData(TransactionType.Expense)]
    [InlineData(TransactionType.Income)]
    public async Task ResolveAsync_ReturnsParent_ForEitherType(TransactionType type)
    {
        var appData = Substitute.For<IAppDataService>();
        appData.GetTransactionByIdAsync(3, Arg.Any<CancellationToken>()).Returns(new Transaction
        {
            Id = 3,
            Type = type,
            Name = "Parent",
            AccountId = 1,
            Account = new Account { Id = 1, Name = "Checking" }
        });

        var result = await TransactionDetailTargetResolver.ResolveAsync(
            new TransactionVM { Id = 4, ParentTransactionId = 3 }, appData);

        Assert.Equal(3, result.Id);
        Assert.Equal(type, result.Type);
        Assert.Equal("Parent", result.Name);
    }
}
