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
            new() { Name = "NeedsThreshold", Value = "40" },
            new() { Name = "WantsThreshold", Value = "40" },
            new() { Name = "InvestThreshold", Value = "20" },
            new() { Name = "AllocationPeriod", Value = AllocationPeriod.Yearly.ToString() },
            new() { Name = UserSettingNames.IsCreditDeadlineNotifEnabled, Value = "False" },
            new() { Name = UserSettingNames.IsGoalDeadlineNotifEnabled, Value = "True" }
        };

        var (removedSettingNames, upsertSettingValues) = SettingsVM.BuildSettingsResetPlan(existingSettings);

        Assert.Contains(UserSettingNames.PreferredDisplayName, removedSettingNames);
        Assert.Contains(UserSettingNames.Salary, removedSettingNames);
        Assert.Contains("NeedsThreshold", removedSettingNames);
        Assert.Contains("WantsThreshold", removedSettingNames);
        Assert.Contains("InvestThreshold", removedSettingNames);
        Assert.Contains("AllocationPeriod", removedSettingNames);
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
        Assert.Equal("24", upsertSettingValues[UserSettingNames.NotificationsSnoozePeriod]);
        Assert.DoesNotContain("AllocationPeriod", upsertSettingValues.Keys);
    }

    [Fact]
    public async Task ResetBudgetAllocationToDefaultsAsync_ResetsTypedBudgetAllocation()
    {
        var appData = Substitute.For<IAppDataService>();
        var allocation = new BudgetAllocation
        {
            NeedsThreshold = 25,
            WantsThreshold = 25,
            InvestThreshold = 50,
            AllocationLimit = 900m,
            AllocationPeriod = AllocationPeriod.Yearly,
            NeedsDebt = 11m,
            WantsDebt = 22m,
            InvestDebt = 33m,
            RolloverPolicy = RolloverPolicy.Pooled,
            OverspendPolicy = OverspendPolicy.SoftDebt
        };
        appData.GetBudgetAllocationAsync(Arg.Any<CancellationToken>())
            .Returns(allocation);

        await SettingsVM.ResetBudgetAllocationToDefaultsAsync(appData);

        Assert.Equal(50, allocation.NeedsThreshold);
        Assert.Equal(30, allocation.WantsThreshold);
        Assert.Equal(20, allocation.InvestThreshold);
        Assert.Equal(0m, allocation.AllocationLimit);
        Assert.Equal(AllocationPeriod.Monthly, allocation.AllocationPeriod);
        Assert.Equal(0m, allocation.NeedsDebt);
        Assert.Equal(0m, allocation.WantsDebt);
        Assert.Equal(0m, allocation.InvestDebt);
        Assert.Equal(RolloverPolicy.None, allocation.RolloverPolicy);
        Assert.Equal(OverspendPolicy.Ignore, allocation.OverspendPolicy);
        appData.Received(1).UpdateBudgetAllocation(allocation);
    }

    [Fact]
    public async Task ApplyDeleteAllDataBudgetAllocationPolicyAsync_ResetsOnlyWhenSettingsAreNotPreserved()
    {
        var appData = Substitute.For<IAppDataService>();
        var allocation = new BudgetAllocation
        {
            NeedsThreshold = 10,
            WantsThreshold = 80,
            InvestThreshold = 10,
            AllocationPeriod = AllocationPeriod.Quarterly
        };
        appData.GetBudgetAllocationAsync(Arg.Any<CancellationToken>())
            .Returns(allocation);

        await SettingsVM.ApplyDeleteAllDataBudgetAllocationPolicyAsync(appData, keepSettings: true);

        Assert.Equal(10, allocation.NeedsThreshold);
        Assert.Equal(80, allocation.WantsThreshold);
        Assert.Equal(10, allocation.InvestThreshold);
        Assert.Equal(AllocationPeriod.Quarterly, allocation.AllocationPeriod);
        appData.DidNotReceive().UpdateBudgetAllocation(Arg.Any<BudgetAllocation>());

        await SettingsVM.ApplyDeleteAllDataBudgetAllocationPolicyAsync(appData, keepSettings: false);

        Assert.Equal(50, allocation.NeedsThreshold);
        Assert.Equal(30, allocation.WantsThreshold);
        Assert.Equal(20, allocation.InvestThreshold);
        Assert.Equal(AllocationPeriod.Monthly, allocation.AllocationPeriod);
        appData.Received(1).UpdateBudgetAllocation(allocation);
    }

    [Fact]
    public void ShouldDeleteTagOnDeleteAllData_DeletesOnlyNonSystemTags()
    {
        var systemTag = new Tag
        {
            Id = 1,
            Name = "System",
            HexCode = "#FFFFFF",
            IsSystemTag = true
        };

        var customTag = new Tag
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

        var systemTag = new Tag { Id = 1, Name = "System", HexCode = "#FFFFFF", IsSystemTag = true };
        var customTag = new Tag { Id = 2, Name = "Custom", HexCode = "#000000", IsSystemTag = false };

        var source = new Account { Id = 1, Name = "Wallet" };
        var expense = new Expense
        {
            Id = 1,
            Name = "Coffee",
            AccountId = source.Id,
            Account = source,
            TagId = customTag.Id,
            Tag = customTag
        };

        var expenseLog = new ExpenseLog
        {
            Id = 1,
            ExpenseId = expense.Id,
            Expense = expense,
            AccountId = source.Id,
            Account = source,
            Notes = string.Empty
        };

        var incomeLog = new IncomeLog
        {
            Id = 1,
            AccountId = source.Id,
            Account = source,
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

        appData.DidNotReceive().RemoveTag(systemTag);
        appData.Received(1).RemoveRecurringTransaction(recurringTransaction);
        appData.Received(1).RemoveTag(customTag);
        appData.Received(1).RemoveAccount(source);
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
            appData.RemoveAccount(source);
            appData.RemoveSavingGoal(savingGoal);
            appData.RemoveTag(customTag);
            appData.RemoveNotification(firstNotification);
            appData.RemoveNotification(secondNotification);
        });
    }

    [Fact]
    public async Task EnsureDeleteAllDataSystemTagsAsync_AddsMissingSystemTags_ByNameAndColor()
    {
        var appData = Substitute.For<IAppDataService>();
        appData.AddTagAsync(Arg.Any<Tag>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var existingTags = new List<Tag>
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

        await appData.Received(1).AddTagAsync(
            Arg.Is<Tag>(tag =>
                tag.Name == SystemTags.BalanceUpdateName &&
                tag.HexCode == SystemTags.BalanceUpdateHexCode &&
                tag.IsSystemTag),
            Arg.Any<CancellationToken>());

        await appData.Received(1).AddTagAsync(
            Arg.Is<Tag>(tag =>
                tag.Name == SystemTags.DataRestorationName &&
                tag.HexCode == SystemTags.DataRestorationHexCode &&
                tag.IsSystemTag),
            Arg.Any<CancellationToken>());

        await appData.Received(1).AddTagAsync(
            Arg.Is<Tag>(tag =>
                tag.Name == SystemTags.BudgetReconciliationName &&
                tag.HexCode == SystemTags.BudgetReconciliationHexCode &&
                tag.IsSystemTag),
            Arg.Any<CancellationToken>());

        await appData.DidNotReceive().AddTagAsync(
            Arg.Is<Tag>(tag => tag.Name == SystemTags.GoalUpdateName),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnsureDeleteAllDataSystemTagsAsync_UpdatesExistingSystemTagColor_WhenColorDiffers()
    {
        var appData = Substitute.For<IAppDataService>();
        var dataRestorationTag = new Tag
        {
            Id = 3,
            Name = SystemTags.DataRestorationName,
            HexCode = "#e9c178",
            IsSystemTag = true
        };

        await SettingsVM.EnsureDeleteAllDataSystemTagsAsync(appData, [dataRestorationTag]);

        appData.Received(1).UpdateTag(Arg.Is<Tag>(tag =>
            tag.Id == dataRestorationTag.Id &&
            tag.Name == SystemTags.DataRestorationName &&
            tag.HexCode == SystemTags.DataRestorationHexCode &&
            tag.IsSystemTag));
    }
}
