using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.Services.History;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsPersonalizationTabVM : ObservableObject
{
    private const string DefaultCurrencyCode = "USD";

    private readonly IMessenger _messenger;
    private readonly Dictionary<string, bool> _savedNotificationSettings = new(StringComparer.Ordinal);
    private readonly IUnitOfWork _unitOfWork;
    private string _savedCurrencyCode = DefaultCurrencyCode;
    private string _savedPreferredAppName = string.Empty;

    [ObservableProperty] private string _preferredAppName = string.Empty;
    [ObservableProperty] private string _selectedCurrencyCode = DefaultCurrencyCode;

    public SettingsPersonalizationTabVM(IUnitOfWork unitOfWork, IMessenger? messenger = null)
    {
        _unitOfWork = unitOfWork;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
        SettingsShared.ReplaceCollection(CurrencyOptions, BuildCurrencyOptions());
    }

    public ObservableCollection<SettingsNotificationOptionVM> NotificationSettings { get; } = [];
    public ObservableCollection<SettingsCurrencyOptionVM> CurrencyOptions { get; } = [];

    public bool HasPendingChanges =>
        !string.Equals((PreferredAppName ?? string.Empty).Trim(), _savedPreferredAppName, StringComparison.Ordinal) ||
        !string.Equals(SelectedCurrencyCode, _savedCurrencyCode, StringComparison.Ordinal) ||
        NotificationSettings.Any(setting =>
            _savedNotificationSettings.TryGetValue(setting.SettingName, out var savedValue)
                ? savedValue != setting.IsEnabled
                : setting.IsEnabled);

    public string SelectedCurrencySymbol =>
        CurrencyOptions.FirstOrDefault(option =>
            string.Equals(option.Code, SelectedCurrencyCode, StringComparison.OrdinalIgnoreCase))?.Symbol ?? "$";

    public async Task LoadAsync()
    {
        var settingsByName = await SettingsShared.GetSettingsDictionaryAsync(_unitOfWork);
        PreferredAppName = SettingsShared.ParseString(settingsByName, UserSettingNames.PreferredDisplayName, string.Empty);
        _savedPreferredAppName = (PreferredAppName ?? string.Empty).Trim();
        SelectedCurrencyCode = ParseCurrencyCode(settingsByName, UserSettingNames.PreferredCurrencyCode, DefaultCurrencyCode);
        _savedCurrencyCode = SelectedCurrencyCode;
        LoadNotificationSettings(settingsByName);
        PublishPendingState();
    }

    public async Task<(SettingsOperationResult Result, List<ILogMemoryAction> Actions, string? OldUsername, string? NewUsername)>
        BuildApplyChangesAsync()
    {
        var actions = new List<ILogMemoryAction>();

        await SettingsShared.UpdateUserSettingAsync(_unitOfWork, UserSettingNames.PreferredDisplayName,
            string.IsNullOrWhiteSpace(PreferredAppName) ? null : PreferredAppName.Trim(), actions);
        await SettingsShared.UpdateUserSettingAsync(_unitOfWork, UserSettingNames.PreferredCurrencyCode,
            SelectedCurrencyCode, actions);

        foreach (var notificationSetting in NotificationSettings)
            await SettingsShared.UpdateUserSettingAsync(_unitOfWork, notificationSetting.SettingName,
                notificationSetting.IsEnabled.ToString(CultureInfo.InvariantCulture), actions);

        var newUsername = string.IsNullOrWhiteSpace(PreferredAppName) ? "User" : PreferredAppName.Trim();
        var oldUsername = string.IsNullOrWhiteSpace(_savedPreferredAppName) ? "User" : _savedPreferredAppName;
        return (SettingsOperationResult.Success(), actions, oldUsername, newUsername);
    }

    public void CommitSavedState()
    {
        _savedPreferredAppName = (PreferredAppName ?? string.Empty).Trim();
        _savedCurrencyCode = SelectedCurrencyCode;
        _savedNotificationSettings.Clear();
        foreach (var setting in NotificationSettings)
            _savedNotificationSettings[setting.SettingName] = setting.IsEnabled;
        PublishPendingState();
    }

    public void RevertChanges()
    {
        PreferredAppName = _savedPreferredAppName;
        SelectedCurrencyCode = _savedCurrencyCode;
        foreach (var setting in NotificationSettings)
            if (_savedNotificationSettings.TryGetValue(setting.SettingName, out var value))
                setting.IsEnabled = value;
        PublishPendingState();
    }

    partial void OnPreferredAppNameChanged(string value)
    {
        PublishPendingState();
    }

    partial void OnSelectedCurrencyCodeChanged(string value)
    {
        if (CurrencyOptions.All(option => !string.Equals(option.Code, value, StringComparison.OrdinalIgnoreCase)))
        {
            var fallbackCode = CurrencyOptions.FirstOrDefault()?.Code ?? DefaultCurrencyCode;
            if (!string.Equals(SelectedCurrencyCode, fallbackCode, StringComparison.Ordinal))
                SelectedCurrencyCode = fallbackCode;
            return;
        }

        OnPropertyChanged(nameof(SelectedCurrencySymbol));
        PublishPendingState();
    }

    private void LoadNotificationSettings(IReadOnlyDictionary<string, string> settingsByName)
    {
        foreach (var notificationSetting in NotificationSettings)
            notificationSetting.PropertyChanged -= OnNotificationSettingPropertyChanged;

        SettingsShared.ReplaceCollection(NotificationSettings,
        [
            new SettingsNotificationOptionVM(
                "Upcoming fixed expense reminders",
                "Warn before recurring fixed expenses are due.",
                UserSettingNames.IsFixedExpensesDeductionNotifEnabled,
                SettingsShared.ParseBool(settingsByName, UserSettingNames.IsFixedExpensesDeductionNotifEnabled, false)),
            new SettingsNotificationOptionVM(
                "Credit deadline reminders",
                "Warn when credit and BNPL due dates are approaching.",
                UserSettingNames.IsCreditDeadlineNotifEnabled,
                SettingsShared.ParseBool(settingsByName, UserSettingNames.IsCreditDeadlineNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Goal deadline alerts",
                "Warn when a savings goal is close to its saving end date.",
                UserSettingNames.IsGoalDeadlineNotifEnabled,
                SettingsShared.ParseBool(settingsByName, UserSettingNames.IsGoalDeadlineNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Late payment alerts",
                "Warn when credit and BNPL payments are past due.",
                UserSettingNames.IsLatePaymentNotifEnabled,
                SettingsShared.ParseBool(settingsByName, UserSettingNames.IsLatePaymentNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Budget threshold alerts",
                "Warn when Needs, Wants, or Invest allocations are nearly spent.",
                UserSettingNames.IsBudgetThresholdNotifEnabled,
                SettingsShared.ParseBool(settingsByName, UserSettingNames.IsBudgetThresholdNotifEnabled, true)),
            new SettingsNotificationOptionVM(
                "Low credit usage alerts",
                "Warn when credit or BNPL sources cross their usage threshold.",
                UserSettingNames.IsLowCreditNotifEnabled,
                SettingsShared.ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false)),
            new SettingsNotificationOptionVM(
                "Low account balance alerts",
                "Warn when checking or cash sources are running low.",
                UserSettingNames.IsLowAccountBalanceNotifEnabled,
                SettingsShared.ParseBool(settingsByName, UserSettingNames.IsLowAccountBalanceNotifEnabled,
                    SettingsShared.ParseBool(settingsByName, UserSettingNames.IsLowCreditNotifEnabled, false)))
        ]);

        _savedNotificationSettings.Clear();
        foreach (var notificationSetting in NotificationSettings)
        {
            notificationSetting.PropertyChanged += OnNotificationSettingPropertyChanged;
            _savedNotificationSettings[notificationSetting.SettingName] = notificationSetting.IsEnabled;
        }
    }

    private void OnNotificationSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsNotificationOptionVM.IsEnabled))
            PublishPendingState();
    }

    private string ParseCurrencyCode(IReadOnlyDictionary<string, string> settings, string name, string defaultValue)
    {
        var code = SettingsShared.ParseString(settings, name, defaultValue).ToUpperInvariant();
        if (CurrencyOptions.Any(option => string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase)))
            return code;

        return defaultValue;
    }

    private static IReadOnlyList<SettingsCurrencyOptionVM> BuildCurrencyOptions()
    {
        return
        [
            new SettingsCurrencyOptionVM("USD", "US Dollar", "$"),
            new SettingsCurrencyOptionVM("EUR", "Euro", "EUR"),
            new SettingsCurrencyOptionVM("GBP", "British Pound", "GBP"),
            new SettingsCurrencyOptionVM("JPY", "Japanese Yen", "JPY"),
            new SettingsCurrencyOptionVM("THB", "Thai Baht", "THB"),
            new SettingsCurrencyOptionVM("AUD", "Australian Dollar", "A$"),
            new SettingsCurrencyOptionVM("CAD", "Canadian Dollar", "C$"),
            new SettingsCurrencyOptionVM("SGD", "Singapore Dollar", "S$"),
            new SettingsCurrencyOptionVM("VND", "Vietnamese Dong", "VND"),
            new SettingsCurrencyOptionVM("INR", "Indian Rupee", "INR")
        ];
    }

    private void PublishPendingState()
    {
        _messenger.Send(new SettingsPendingChangesChangedMessage(
            new SettingsPendingChangesChanged(SettingsTabKey.Personalization, HasPendingChanges)));
    }
}
