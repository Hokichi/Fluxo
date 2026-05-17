using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Services.History;
using Fluxo.Services.Updates;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsPersonalizationTabVM : ObservableObject
{
    private readonly IMessenger _messenger;
    private readonly Dictionary<string, bool> _savedNotificationSettings = new(StringComparer.Ordinal);
    private readonly IAppDataService _appData;
    private readonly IAppUpdateService _appUpdateService;
    private string _savedPreferredAppName = string.Empty;
    private bool _savedShouldRunAtStartup;
    private AppCloseBehavior _savedCloseBehavior = AppCloseBehavior.Exit;

    [ObservableProperty] private string _preferredAppName = string.Empty;
    [ObservableProperty] private bool _shouldRunAtStartup;
    [ObservableProperty] private AppCloseBehavior _closeBehavior = AppCloseBehavior.Exit;
    [ObservableProperty] private bool _isCheckingForUpdates;

    public SettingsPersonalizationTabVM(
        IAppDataService appData,
        IMessenger? messenger = null,
        IAppUpdateService? appUpdateService = null)
    {
        _appData = appData;
        _appUpdateService = appUpdateService ?? new AppUpdateService();
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public ObservableCollection<SettingsNotificationOptionVM> NotificationSettings { get; } = [];

    public bool IsMinimizeToTrayCloseBehaviorSelected => CloseBehavior == AppCloseBehavior.MinimizeToTray;

    public bool IsExitCloseBehaviorSelected => CloseBehavior == AppCloseBehavior.Exit;

    public string CurrentVersion { get; } = ResolveCurrentVersion();

    public bool HasPendingChanges =>
        !string.Equals((PreferredAppName ?? string.Empty).Trim(), _savedPreferredAppName, StringComparison.Ordinal) ||
        ShouldRunAtStartup != _savedShouldRunAtStartup ||
        CloseBehavior != _savedCloseBehavior ||
        NotificationSettings.Any(setting =>
            _savedNotificationSettings.TryGetValue(setting.SettingName, out var savedValue)
                ? savedValue != setting.IsEnabled
                : setting.IsEnabled);

    public async Task LoadAsync()
    {
        var settingsByName = await SettingsShared.GetSettingsDictionaryAsync(_appData);
        PreferredAppName = SettingsShared.ParseString(settingsByName, UserSettingNames.PreferredDisplayName, string.Empty);
        ShouldRunAtStartup = SettingsShared.ParseBool(settingsByName, UserSettingNames.ShouldRunAtStartup, false);
        CloseBehavior = SettingsShared.ParseCloseBehavior(settingsByName, UserSettingNames.CloseBehavior, AppCloseBehavior.Exit);
        _savedPreferredAppName = (PreferredAppName ?? string.Empty).Trim();
        _savedShouldRunAtStartup = ShouldRunAtStartup;
        _savedCloseBehavior = CloseBehavior;
        LoadNotificationSettings(settingsByName);
        PublishPendingState();
    }

    public Task<SettingsMaintenanceResult> ResetAllSettingsAsync()
    {
        return SendMaintenanceRequestAsync(SettingsMaintenanceRequestType.ResetAllSettings, keepSettings: true);
    }

    public Task<SettingsMaintenanceResult> DeleteAllDataAsync(bool keepSettings)
    {
        return SendMaintenanceRequestAsync(SettingsMaintenanceRequestType.DeleteAllData, keepSettings);
    }

    public void RequestClosePopup()
    {
        _messenger.Send(new SettingsPopupCloseRequestedMessage(new SettingsPopupCloseRequest()));
    }

    public async Task<AppUpdateCheckResult> CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;
        try
        {
            return await _appUpdateService.CheckForUpdatesAsync(CurrentVersion);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    public Task<string> DownloadUpdateInstallerAsync(AppUpdateCheckResult update)
    {
        if (update.Status != AppUpdateCheckStatus.UpdateAvailable
            || string.IsNullOrWhiteSpace(update.InstallerDownloadUrl)
            || string.IsNullOrWhiteSpace(update.InstallerAssetName))
        {
            throw new InvalidOperationException("No Fluxo update installer is available to download.");
        }

        return _appUpdateService.DownloadInstallerAsync(
            update.InstallerDownloadUrl,
            update.InstallerAssetName);
    }

    public void DeleteDownloadedInstaller(string installerPath)
    {
        _appUpdateService.DeleteInstaller(installerPath);
    }

    public async Task<(
        SettingsOperationResult Result,
        List<ILogMemoryAction> Actions,
        string? OldUsername,
        string? NewUsername,
        bool ShouldRunAtStartup)>
        BuildApplyChangesAsync()
    {
        var actions = new List<ILogMemoryAction>();

        await SettingsShared.UpdateUserSettingAsync(_appData, UserSettingNames.PreferredDisplayName,
            string.IsNullOrWhiteSpace(PreferredAppName) ? null : PreferredAppName.Trim(), actions);

        foreach (var notificationSetting in NotificationSettings)
            await SettingsShared.UpdateUserSettingAsync(_appData, notificationSetting.SettingName,
                notificationSetting.IsEnabled.ToString(CultureInfo.InvariantCulture), actions);

        await SettingsShared.UpdateUserSettingAsync(_appData, UserSettingNames.ShouldRunAtStartup,
            ShouldRunAtStartup.ToString(CultureInfo.InvariantCulture), actions);

        await SettingsShared.UpdateUserSettingAsync(_appData, UserSettingNames.CloseBehavior,
            CloseBehavior.ToString(), actions);

        var newUsername = string.IsNullOrWhiteSpace(PreferredAppName) ? "User" : PreferredAppName.Trim();
        var oldUsername = string.IsNullOrWhiteSpace(_savedPreferredAppName) ? "User" : _savedPreferredAppName;
        return (SettingsOperationResult.Success(), actions, oldUsername, newUsername, ShouldRunAtStartup);
    }

    public void CommitSavedState()
    {
        _savedPreferredAppName = (PreferredAppName ?? string.Empty).Trim();
        _savedShouldRunAtStartup = ShouldRunAtStartup;
        _savedCloseBehavior = CloseBehavior;
        _savedNotificationSettings.Clear();
        foreach (var setting in NotificationSettings)
            _savedNotificationSettings[setting.SettingName] = setting.IsEnabled;
        PublishPendingState();
    }

    public void RevertChanges()
    {
        PreferredAppName = _savedPreferredAppName;
        ShouldRunAtStartup = _savedShouldRunAtStartup;
        CloseBehavior = _savedCloseBehavior;
        foreach (var setting in NotificationSettings)
            if (_savedNotificationSettings.TryGetValue(setting.SettingName, out var value))
                setting.IsEnabled = value;
        PublishPendingState();
    }

    partial void OnPreferredAppNameChanged(string value)
    {
        PublishPendingState();
    }

    partial void OnShouldRunAtStartupChanged(bool value)
    {
        PublishPendingState();
    }

    partial void OnCloseBehaviorChanged(AppCloseBehavior value)
    {
        OnPropertyChanged(nameof(IsMinimizeToTrayCloseBehaviorSelected));
        OnPropertyChanged(nameof(IsExitCloseBehaviorSelected));
        PublishPendingState();
    }

    [RelayCommand]
    private void SetCloseBehavior(AppCloseBehavior closeBehavior)
    {
        CloseBehavior = closeBehavior;
    }

    private static string ResolveCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var metadataIndex = informationalVersion.IndexOf('+');
            return metadataIndex > 0
                ? informationalVersion[..metadataIndex]
                : informationalVersion;
        }

        var version = assembly.GetName().Version;
        if (version is null)
            return "Unknown";

        var build = version.Build >= 0 ? version.Build : 0;
        return $"{version.Major}.{version.Minor}.{build}";
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

    private void PublishPendingState()
    {
        _messenger.Send(new SettingsPendingChangesChangedMessage(
            new SettingsPendingChangesChanged(SettingsTabKey.Personalization, HasPendingChanges)));
    }

    private async Task<SettingsMaintenanceResult> SendMaintenanceRequestAsync(SettingsMaintenanceRequestType requestType,
        bool keepSettings)
    {
        var completionSource = new TaskCompletionSource<SettingsMaintenanceResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _messenger.Send(new SettingsMaintenanceRequestedMessage(
            new SettingsMaintenanceRequest(requestType, keepSettings, completionSource)));

        var completedTask = await Task.WhenAny(completionSource.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        return completedTask == completionSource.Task
            ? await completionSource.Task
            : SettingsMaintenanceResult.Failure("Unable to run this settings action.");
    }
}

