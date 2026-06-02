using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Popups.Settings;
using NSubstitute;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups.Settings;

public sealed class SettingsVMMaintenancePolicyTests
{
    [Fact]
    public void BuildSettingsResetPlan_RemovesNonNotificationSettings_AndUpsertsNotificationDefaults()
    {
        var existingSettings = new List<UserSettings>
        {
            new() { Name = UserSettingNames.PreferredDisplayName, Value = "Alex" },
            new() { Name = UserSettingNames.Salary, Value = "5000" },
            new() { Name = UserSettingNames.IsCreditDeadlineNotifEnabled, Value = "False" },
            new() { Name = UserSettingNames.IsGoalDeadlineNotifEnabled, Value = "True" }
        };

        var (removedSettingNames, upsertSettingValues) = SettingsVM.BuildSettingsResetPlan(existingSettings);

        Assert.Contains(UserSettingNames.PreferredDisplayName, removedSettingNames);
        Assert.Contains(UserSettingNames.Salary, removedSettingNames);
        Assert.DoesNotContain(UserSettingNames.IsCreditDeadlineNotifEnabled, removedSettingNames);
        Assert.DoesNotContain(UserSettingNames.IsGoalDeadlineNotifEnabled, removedSettingNames);

        Assert.Equal(10, upsertSettingValues.Count);
        Assert.Equal("True", upsertSettingValues[UserSettingNames.IsFixedExpensesDeductionNotifEnabled]);
        Assert.Equal("True", upsertSettingValues[UserSettingNames.IsCreditDeadlineNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsGoalDeadlineNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsLatePaymentNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsBudgetThresholdNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsLowCreditNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsLowAccountBalanceNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.ShouldRunAtStartup]);
        Assert.Equal("Exit", upsertSettingValues[UserSettingNames.CloseBehavior]);
        Assert.Equal(AllocationPeriod.Monthly.ToString(), upsertSettingValues[UserSettingNames.AllocationPeriod]);
    }

    [Fact]
    public void ShouldDeleteTagOnDeleteAllData_DeletesOnlyNonSystemTags()
    {
        var systemTag = new ExpenseTag
        {
            Id = 1,
            Name = "System",
            HexCode = "#FFFFFF",
            IsSystemTag = true
        };

        var customTag = new ExpenseTag
        {
            Id = 2,
            Name = "Custom",
            HexCode = "#000000",
            IsSystemTag = false
        };

        Assert.False(SettingsVM.ShouldDeleteTagOnDeleteAllData(systemTag));
        Assert.True(SettingsVM.ShouldDeleteTagOnDeleteAllData(customTag));
    }

    [Fact]
    public void ApplyDeleteAllDataRemovalPolicy_RemovesDependentsBeforePrincipals_AndOnlyNonSystemTags()
    {
        var appData = Substitute.For<IAppDataService>();

        var systemTag = new ExpenseTag { Id = 1, Name = "System", HexCode = "#FFFFFF", IsSystemTag = true };
        var customTag = new ExpenseTag { Id = 2, Name = "Custom", HexCode = "#000000", IsSystemTag = false };

        var source = new SpendingSource { Id = 1, Name = "Wallet" };
        var expense = new Expense
        {
            Id = 1,
            Name = "Coffee",
            SpendingSourceId = source.Id,
            SpendingSource = source,
            ExpenseTagId = customTag.Id,
            ExpenseTag = customTag
        };

        var expenseLog = new ExpenseLog
        {
            Id = 1,
            ExpenseId = expense.Id,
            Expense = expense,
            SpendingSourceId = source.Id,
            SpendingSource = source,
            Notes = string.Empty
        };

        var incomeLog = new IncomeLog
        {
            Id = 1,
            SpendingSourceId = source.Id,
            SpendingSource = source,
            Notes = string.Empty
        };

        var savingGoal = new SavingGoal { Id = 1, Name = "Emergency" };
        var recurringTransaction = new RecurringTransaction
        {
            Id = 1,
            Name = "Rent",
            SourceId = source.Id,
            Source = source,
            TagId = customTag.Id,
            Tag = customTag,
            GoalId = savingGoal.Id,
            Goal = savingGoal
        };
        var firstNotification = new Notification { Id = 1, Type = "A", Header = "H1", Message = "M1" };
        var secondNotification = new Notification { Id = 2, Type = "B", Header = "H2", Message = "M2" };

        SettingsVM.ApplyDeleteAllDataRemovalPolicy(
            appData,
            [systemTag, customTag],
            [source],
            [expense],
            [expenseLog],
            [incomeLog],
            [savingGoal],
            [recurringTransaction],
            [firstNotification, secondNotification]);

        appData.DidNotReceive().RemoveExpenseTag(systemTag);
        appData.Received(1).RemoveRecurringTransaction(recurringTransaction);
        appData.Received(1).RemoveExpenseTag(customTag);
        appData.Received(1).RemoveSpendingSource(source);
        appData.Received(1).RemoveExpense(expense);
        appData.Received(1).RemoveExpenseLog(expenseLog);
        appData.Received(1).RemoveIncomeLog(incomeLog);
        appData.Received(1).RemoveSavingGoal(savingGoal);
        appData.Received(1).RemoveNotification(firstNotification);
        appData.Received(1).RemoveNotification(secondNotification);

        Received.InOrder(() =>
        {
            appData.RemoveRecurringTransaction(recurringTransaction);
            appData.RemoveExpenseLog(expenseLog);
            appData.RemoveExpense(expense);
            appData.RemoveIncomeLog(incomeLog);
            appData.RemoveSpendingSource(source);
            appData.RemoveSavingGoal(savingGoal);
            appData.RemoveExpenseTag(customTag);
            appData.RemoveNotification(firstNotification);
            appData.RemoveNotification(secondNotification);
        });
    }

    [Fact]
    public async Task EnsureDeleteAllDataSystemTagsAsync_AddsMissingSystemTags_ByNameAndColor()
    {
        var appData = Substitute.For<IAppDataService>();
        appData.AddExpenseTagAsync(Arg.Any<ExpenseTag>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var existingTags = new List<ExpenseTag>
        {
            new()
            {
                Id = 1,
                Name = "Balance Update",
                HexCode = "#000000",
                IsSystemTag = false
            },
            new()
            {
                Id = 2,
                Name = "Goal Update",
                HexCode = "#eaabf2",
                IsSystemTag = true
            }
        };

        await SettingsVM.EnsureDeleteAllDataSystemTagsAsync(appData, existingTags);

        await appData.Received(1).AddExpenseTagAsync(
            Arg.Is<ExpenseTag>(tag =>
                tag.Name == "Balance Update" &&
                tag.HexCode == "#a3e5d6" &&
                tag.IsSystemTag),
            Arg.Any<CancellationToken>());

        await appData.Received(1).AddExpenseTagAsync(
            Arg.Is<ExpenseTag>(tag =>
                tag.Name == "Data Restoration" &&
                tag.HexCode == "#123456" &&
                tag.IsSystemTag),
            Arg.Any<CancellationToken>());

        await appData.DidNotReceive().AddExpenseTagAsync(
            Arg.Is<ExpenseTag>(tag => tag.Name == "Goal Update"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureDeleteAllDataSystemTagsAsync_UpdatesExistingSystemTagColor_WhenColorDiffers()
    {
        var appData = Substitute.For<IAppDataService>();
        var dataRestorationTag = new ExpenseTag
        {
            Id = 3,
            Name = "Data Restoration",
            HexCode = "#e9c178",
            IsSystemTag = true
        };

        await SettingsVM.EnsureDeleteAllDataSystemTagsAsync(appData, [dataRestorationTag]);

        appData.Received(1).UpdateExpenseTag(Arg.Is<ExpenseTag>(tag =>
            tag.Id == dataRestorationTag.Id &&
            tag.Name == "Data Restoration" &&
            tag.HexCode == "#123456" &&
            tag.IsSystemTag));
    }
}
