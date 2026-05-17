using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
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

        Assert.Equal(9, upsertSettingValues.Count);
        Assert.Equal("True", upsertSettingValues[UserSettingNames.IsFixedExpensesDeductionNotifEnabled]);
        Assert.Equal("True", upsertSettingValues[UserSettingNames.IsCreditDeadlineNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsGoalDeadlineNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsLatePaymentNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsBudgetThresholdNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsLowCreditNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsLowAccountBalanceNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.ShouldRunAtStartup]);
        Assert.Equal("Exit", upsertSettingValues[UserSettingNames.CloseBehavior]);
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
    public void ApplyDeleteAllDataRemovalPolicy_RemovesAllNotifications_AndOnlyNonSystemTags()
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
            [firstNotification, secondNotification]);

        appData.DidNotReceive().RemoveExpenseTag(systemTag);
        appData.Received(1).RemoveExpenseTag(customTag);
        appData.Received(1).RemoveSpendingSource(source);
        appData.Received(1).RemoveExpense(expense);
        appData.Received(1).RemoveExpenseLog(expenseLog);
        appData.Received(1).RemoveIncomeLog(incomeLog);
        appData.Received(1).RemoveSavingGoal(savingGoal);
        appData.Received(1).RemoveNotification(firstNotification);
        appData.Received(1).RemoveNotification(secondNotification);
    }
}
