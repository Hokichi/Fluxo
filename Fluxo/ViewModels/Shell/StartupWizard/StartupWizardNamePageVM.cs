using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardNamePageVM : ObservableObject
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessenger _messenger;

    [ObservableProperty] private string _usernameText = "User";

    public StartupWizardNamePageVM(IUnitOfWork unitOfWork, IMessenger? messenger = null)
    {
        _unitOfWork = unitOfWork;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public string ResolvedUsername => StartupWizardShared.ResolveUsername(UsernameText);

    public async Task LoadAsync()
    {
        var settings = await _unitOfWork.UserSettings.GetAllAsync();
        var settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

        UsernameText = StartupWizardShared.ParseString(settingsByName, UserSettingNames.PreferredDisplayName, "User");

        PublishIdentitySnapshot();
    }

    public async Task<SettingsOperationResult> SaveAsync()
    {
        await StartupWizardShared.UpsertUserSettingAsync(_unitOfWork, UserSettingNames.PreferredDisplayName, ResolvedUsername);
        await _unitOfWork.SaveChangesAsync();

        _messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.All));
        PublishIdentitySnapshot();
        return SettingsOperationResult.Success();
    }

    partial void OnUsernameTextChanged(string value)
    {
        OnPropertyChanged(nameof(ResolvedUsername));
        PublishIdentitySnapshot();
    }

    public string CurrentStepTitle => "What should Fluxo call you?";

    public string CurrentStepDescription => "Pick the name you'd like Fluxo to use throughout the app.";

    private void PublishIdentitySnapshot()
    {
        _messenger.Send(new StartupWizardIdentityChangedMessage(
            new StartupWizardIdentityChanged(ResolvedUsername)));
    }
}