using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class TransactionDetailVMBalloonToggleTests
{
    private static readonly string ViewModelPath = RepositoryPaths.File(
        "Fluxo", "ViewModels", "Popups", "TransactionDetailVM.cs");

    [Fact]
    public void Commands_SetOnlySupportedUpdateModes()
    {
        var source = File.ReadAllText(ViewModelPath);

        Assert.Contains("public void HandleIoUModeClick()", source);
        Assert.Contains("public void HandleExcludeModeClick()", source);
        Assert.Contains("public void HandleExcludedIoUModeClick()", source);
        Assert.Contains("private void ClearTransactionModes()", source);
        Assert.Contains("IsIoU = true;", source);
        Assert.Contains("IsExcludedFromBudget = true;", source);
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
}
