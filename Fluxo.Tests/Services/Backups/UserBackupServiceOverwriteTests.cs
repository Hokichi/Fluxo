using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Services.Backups;
using Xunit;

namespace Fluxo.Tests.Services.Backups;

public sealed class UserBackupServiceOverwriteTests
{
    [Fact]
    public void OverwriteRemovalOrder_RemovesDependentsBeforePrincipals()
    {
        var order = UserBackupService.BuildOverwriteRemovalLabels(
            new UserBackupSelection(new HashSet<DataManagementEntityKind>
            {
                DataManagementEntityKind.SpendingSources,
                DataManagementEntityKind.Expenses,
                DataManagementEntityKind.Incomes,
                DataManagementEntityKind.RecurringTransactions
            }));

        Assert.True(order.IndexOf("RecurringTransactions") < order.IndexOf("SpendingSources"));
        Assert.True(order.IndexOf("ExpenseLogs") < order.IndexOf("SpendingSources"));
        Assert.True(order.IndexOf("Expenses") < order.IndexOf("SpendingSources"));
        Assert.True(order.IndexOf("IncomeLogs") < order.IndexOf("SpendingSources"));
    }

    [Fact]
    public void OverwriteRemovalOrder_SelectingTagsAlsoRemovesRecurringTransactionsFirst()
    {
        var order = UserBackupService.BuildOverwriteRemovalLabels(
            new UserBackupSelection(new HashSet<DataManagementEntityKind>
            {
                DataManagementEntityKind.Tags
            }));

        Assert.Equal(["RecurringTransactions", "ExpenseTags"], order);
    }

    [Fact]
    public void OverwriteRemovalOrder_SelectingGoalsAlsoRemovesRecurringTransactionsFirst()
    {
        var order = UserBackupService.BuildOverwriteRemovalLabels(
            new UserBackupSelection(new HashSet<DataManagementEntityKind>
            {
                DataManagementEntityKind.Goals
            }));

        Assert.Equal(["RecurringTransactions", "SavingGoals"], order);
    }

    [Fact]
    public void OverwriteRemovalOrder_SelectingSpendingSourcesIncludesDependentRemovals()
    {
        var order = UserBackupService.BuildOverwriteRemovalLabels(
            new UserBackupSelection(new HashSet<DataManagementEntityKind>
            {
                DataManagementEntityKind.SpendingSources
            }));

        Assert.Equal(
            ["RecurringTransactions", "ExpenseLogs", "Expenses", "IncomeLogs", "SpendingSources"],
            order);
    }

    [Fact]
    public void OverwriteRemovalOrder_UserSettingsOnly_RemainsIsolated()
    {
        var order = UserBackupService.BuildOverwriteRemovalLabels(
            new UserBackupSelection(new HashSet<DataManagementEntityKind>
            {
                DataManagementEntityKind.UserSettings
            }));

        Assert.Equal(["UserSettings"], order);
    }
}
