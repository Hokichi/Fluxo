using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardNotificationVM : ObservableObject
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessenger _messenger;

    [ObservableProperty] private bool _isStep6Active;

    public StartupWizardNotificationVM(IUnitOfWork unitOfWork, IMessenger? messenger = null)
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
                StartupWizardShared.ParseBool(settingsByName, UserSettingNames.IsFixedExpensesDeductionNotifEnabled, false)),
            new SettingsNotificationOptionVM(
                "Credit deadline reminders",
                "Warn when credit and BNPL due dates are approaching.",
                UserSettingNames.IsCreditDeadlineNotifEnabled,
                StartupWizardShared.ParseBool(settingsByName, UserSettingNames.IsCreditDeadlineNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Goal deadline alerts",
                "Warn when a savings goal is close to its saving end date.",
                UserSettingNames.IsGoalDeadlineNotifEnabled,
                StartupWizardShared.ParseBool(settingsByName, UserSettingNames.IsGoalDeadlineNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Late payment alerts",
                "Warn when credit and BNPL payments are past due.",
                UserSettingNames.IsLatePaymentNotifEnabled,
                StartupWizardShared.ParseBool(settingsByName, UserSettingNames.IsLatePaymentNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Budget threshold alerts",
                "Warn when Needs, Wants, or Invest allocations are nearly spent.",
                UserSettingNames.IsBudgetThresholdNotifEnabled,
                StartupWizardShared.ParseBool(settingsByName, UserSettingNames.IsBudgetThresholdNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Low credit usage alerts",
                "Warn when credit or BNPL sources cross their usage threshold.",
                UserSettingNames.IsLowCreditNotifEnabled,
                StartupWizardShared.ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false)),
            new SettingsNotificationOptionVM(
                "Low account balance alerts",
                "Warn when checking or cash sources are running low.",
                UserSettingNames.IsLowAccountBalanceNotifEnabled,
                StartupWizardShared.ParseBool(settingsByName, UserSettingNames.IsLowAccountBalanceNotifEnabled, false))
        ]);

        PublishSnapshot();
    }

    public async Task<SettingsOperationResult> SaveAsync()
    {
        foreach (var setting in NotificationSettings)
            await StartupWizardShared.UpsertUserSettingAsync(
                _unitOfWork,
                setting.SettingName,
                setting.IsEnabled.ToString());

        await _unitOfWork.SaveChangesAsync();
        _messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.Notifications));
        PublishSnapshot();
        return SettingsOperationResult.Success();
    }

    private void ReplaceNotificationSettings(IEnumerable<SettingsNotificationOptionVM> options)
    {
        foreach (var existing in NotificationSettings)
            existing.PropertyChanged -= OnNotificationOptionPropertyChanged;

        StartupWizardShared.ReplaceCollection(NotificationSettings, options);

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
        _messenger.Send(new StartupWizardNotificationsChangedMessage(
            new StartupWizardNotificationsChanged(
                EnabledCount: NotificationSettings.Count(setting => setting.IsEnabled),
                TotalCount: NotificationSettings.Count)));
    }
}
