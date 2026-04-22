using Fluxo.Core.Constants;
using Fluxo.Core.Entities;
using Fluxo.ViewModels.Popups.Settings;
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

        Assert.Equal(7, upsertSettingValues.Count);
        Assert.Equal("True", upsertSettingValues[UserSettingNames.IsFixedExpensesDeductionNotifEnabled]);
        Assert.Equal("True", upsertSettingValues[UserSettingNames.IsCreditDeadlineNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsGoalDeadlineNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsLatePaymentNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsBudgetThresholdNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsLowCreditNotifEnabled]);
        Assert.Equal("False", upsertSettingValues[UserSettingNames.IsLowAccountBalanceNotifEnabled]);
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
}
