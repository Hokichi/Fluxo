using CommunityToolkit.Mvvm.ComponentModel;

namespace Fluxo.ViewModels.Popups.Settings;

public partial class SettingsNotificationOptionVM : ObservableObject
{
    [ObservableProperty] private bool _isEnabled;

    public SettingsNotificationOptionVM(string title, string description, string settingName, bool isEnabled)
    {
        Title = title;
        Description = description;
        SettingName = settingName;
        _isEnabled = isEnabled;
    }

    public string Title { get; }
    public string Description { get; }
    public string SettingName { get; }
}
