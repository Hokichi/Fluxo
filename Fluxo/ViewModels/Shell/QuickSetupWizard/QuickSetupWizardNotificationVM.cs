using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public partial class QuickSetupWizardNotificationVM : ObservableObject
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessenger _messenger;

    [ObservableProperty] private bool _isStep6Active;

    public QuickSetupWizardNotificationVM(IUnitOfWork unitOfWork, IMessenger? messenger = null)
    {
        _unitOfWork = unitOfWork;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public ObservableCollection<SettingsNotificationOptionVM> NotificationSettings { get; } = [];

    public async Task LoadAsync()
    {
        var settings = await _unitOfWork.UserSettings.GetAllAsync();
        var settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

        ReplaceNotificationSettings(
        [
            new SettingsNotificationOptionVM(
                "Upcoming fixed expense reminders",
                "Warn before recurring fixed expenses are due.",
                UserSettingNames.IsFixedExpensesDeductionNotifEnabled,
                QuickSetupWizardShared.ParseBool(settingsByName, UserSettingNames.IsFixedExpensesDeductionNotifEnabled, false)),
            new SettingsNotificationOptionVM(
                "Credit deadline reminders",
                "Warn when credit and BNPL due dates are approaching.",
                UserSettingNames.IsCreditDeadlineNotifEnabled,
                QuickSetupWizardShared.ParseBool(settingsByName, UserSettingNames.IsCreditDeadlineNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Goal deadline alerts",
                "Warn when a savings goal is close to its saving end date.",
                UserSettingNames.IsGoalDeadlineNotifEnabled,
                QuickSetupWizardShared.ParseBool(settingsByName, UserSettingNames.IsGoalDeadlineNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Late payment alerts",
                "Warn when credit and BNPL payments are past due.",
                UserSettingNames.IsLatePaymentNotifEnabled,
                QuickSetupWizardShared.ParseBool(settingsByName, UserSettingNames.IsLatePaymentNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Budget threshold alerts",
                "Warn when Needs, Wants, or Invest allocations are nearly spent.",
                UserSettingNames.IsBudgetThresholdNotifEnabled,
                QuickSetupWizardShared.ParseBool(settingsByName, UserSettingNames.IsBudgetThresholdNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Low credit usage alerts",
                "Warn when credit or BNPL sources cross their usage threshold.",
                UserSettingNames.IsLowCreditNotifEnabled,
                QuickSetupWizardShared.ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false)),
            new SettingsNotificationOptionVM(
                "Low account balance alerts",
                "Warn when checking or cash sources are running low.",
                UserSettingNames.IsLowAccountBalanceNotifEnabled,
                QuickSetupWizardShared.ParseBool(settingsByName, UserSettingNames.IsLowAccountBalanceNotifEnabled, false))
        ]);

        PublishSnapshot();
    }

    public async Task<SettingsOperationResult> SaveAsync()
    {
        await ApplyAsync(_unitOfWork);
        await _unitOfWork.SaveChangesAsync();
        _messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.Notifications));
        PublishSnapshot();
        return SettingsOperationResult.Success();
    }

    public async Task ApplyAsync(IUnitOfWork unitOfWork)
    {
        foreach (var setting in NotificationSettings)
            await QuickSetupWizardShared.UpsertUserSettingAsync(
                unitOfWork,
                setting.SettingName,
                setting.IsEnabled.ToString());
    }

    private void ReplaceNotificationSettings(IEnumerable<SettingsNotificationOptionVM> options)
    {
        foreach (var existing in NotificationSettings)
            existing.PropertyChanged -= OnNotificationOptionPropertyChanged;

        QuickSetupWizardShared.ReplaceCollection(NotificationSettings, options);

        foreach (var option in NotificationSettings)
            option.PropertyChanged += OnNotificationOptionPropertyChanged;
    }

    private void OnNotificationOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SettingsNotificationOptionVM.IsEnabled), StringComparison.Ordinal))
            return;

        PublishSnapshot();
    }

    private void PublishSnapshot()
    {
        _messenger.Send(new QuickSetupWizardNotificationsChangedMessage(
            new QuickSetupWizardNotificationsChanged(
                EnabledCount: NotificationSettings.Count(setting => setting.IsEnabled),
                TotalCount: NotificationSettings.Count)));
    }
}
