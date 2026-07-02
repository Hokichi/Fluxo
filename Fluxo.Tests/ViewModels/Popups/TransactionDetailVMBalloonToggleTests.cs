using Fluxo.Tests.TestSupport;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class TransactionDetailVMBalloonToggleTests
{
    private static readonly string ViewModelPath = RepositoryPaths.File(
        "Fluxo", "ViewModels", "Popups", "TransactionDetailVM.cs");

    [Fact]
    public void RegularMode_MapsToInverseOfIoU()
    {
        var source = File.ReadAllText(ViewModelPath);

        Assert.Contains("public bool IsRegularMode", source);
        Assert.Contains("get => !IsIoU;", source);
        Assert.Contains("if (value) IsIoU = false;", source);
        Assert.DoesNotContain("public void HandleIoUModeClick()", source);
        Assert.DoesNotContain("public void HandleExcludeModeClick()", source);
        Assert.DoesNotContain("public void HandleRecurringModeClick()", source);
        Assert.DoesNotContain("public void HandleInstallmentsModeClick()", source);
    }

    [Fact]
    public void SaveAsync_UpdatesExistingTransactionModeFlags()
    {
        var source = File.ReadAllText(ViewModelPath);
        var saveStart = source.IndexOf("public async Task<TransactionDetailSaveResult> SaveAsync", StringComparison.Ordinal);
        var saveEnd = source.IndexOf("public bool HasValidChangesToPersistOnClose", saveStart, StringComparison.Ordinal);
        var saveSource = source[saveStart..saveEnd];

        Assert.Contains("transaction.IsIoU = input.IsIoU;", saveSource);
        Assert.Contains("transaction.IsExcludedFromBudget = input.IsExcludedFromBudget;", saveSource);
        Assert.Contains("_appData.UpdateTransaction(transaction);", saveSource);
        Assert.DoesNotContain("AddTransactionAsync", saveSource);
    }

    [Fact]
    public void ModeFlags_ParticipateInSavedStateAndDirtyTracking()
    {
        var source = File.ReadAllText(ViewModelPath);

        Assert.Contains("input.IsIoU != savedState.IsIoU", source);
        Assert.Contains("input.IsExcludedFromBudget != savedState.IsExcludedFromBudget", source);
        Assert.Contains("IsIoU = _savedState.IsIoU;", source);
        Assert.Contains("IsExcludedFromBudget = _savedState.IsExcludedFromBudget;", source);
    }

    [Fact]
    public void SplitState_IsPreservedWhileParentIsShownAndSavedOnClose()
    {
        var source = File.ReadAllText(ViewModelPath);

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
