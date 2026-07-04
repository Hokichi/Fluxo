using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class TransactionDetailVMBalloonToggleTests
{
    [Theory]
    [InlineData(false, false, "A one-time transaction")]
    [InlineData(true, false, "A transaction marked as debt/IoU but doesn't affect the accounts")]
    [InlineData(true, true, "A transaction marked as debt/IoU and affects the accounts")]
    public void ModeDescription_MatchesBalanceBehavior(bool isIoU, bool posted, string expected)
    {
        Assert.Equal(expected, TransactionDetailVM.GetTransactionModeDescription(isIoU, posted));
    }

    [Fact]
    public void SplitState_IsPreservedWhileParentIsShownAndSavedOnClose()
    {
        var source = File.ReadAllText(Fluxo.Tests.TestSupport.RepositoryPaths.File(
            "Fluxo", "ViewModels", "Popups", "TransactionDetailVM.cs"));

        Assert.Contains("private bool _areSplitRowsLoaded;", source);
        Assert.Contains("public bool HasPendingSplitChanges", source);
        Assert.Contains("if (_areSplitRowsLoaded)", source);
        Assert.Contains("public void ShowParentTransaction()", source);
        Assert.Contains("if (IsSplitMode || HasPendingSplitChanges)", source);
        Assert.Contains("if (HasPendingSplitChanges)", source);
    }

    [Theory]
    [InlineData(1, 1, 100, 95, 10, false, false)]
    [InlineData(1, 2, 100, 90, 10, false, false)]
    [InlineData(1, 2, 100, 95, 10, false, true)]
    [InlineData(1, 2, 100, 95, 10, true, false)]
    public void MaximumSpendingConfirmation_RequiresChangedOverflowingUnapprovedAccount(
        int currentAccountId,
        int destinationAccountId,
        decimal maximumSpending,
        decimal destinationSpending,
        decimal amount,
        bool overflowApproved,
        bool expected)
    {
        Assert.Equal(expected, TransactionDetailVM.RequiresMaximumSpendingConfirmation(
            currentAccountId,
            destinationAccountId,
            maximumSpending,
            destinationSpending,
            amount,
            overflowApproved));
    }

    [Fact]
    public void CalculateAccountSpending_IncludesExcludedButNotDeletedOrSplitParents()
    {
        var transactions = new[]
        {
            new Transaction { Id = 1, SourceAccountId = 2, Type = TransactionType.Expense, Amount = 30m },
            new Transaction { Id = 2, SourceAccountId = 2, Type = TransactionType.Expense, Amount = 20m, IsExcludedFromBudget = true },
            new Transaction { Id = 3, SourceAccountId = 2, Type = TransactionType.Expense, Amount = 50m, IsForDeletion = true },
            new Transaction { Id = 4, SourceAccountId = 3, Type = TransactionType.Expense, Amount = 100m },
            new Transaction { Id = 5, SourceAccountId = 2, Type = TransactionType.Expense, Amount = 10m, ParentTransactionId = 1 }
        };

        Assert.Equal(30m, TransactionDetailVM.CalculateAccountSpending(transactions, accountId: 2));
    }
}
