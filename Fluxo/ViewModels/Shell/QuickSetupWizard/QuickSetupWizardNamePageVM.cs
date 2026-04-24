using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Core.Constants;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces;
using Fluxo.Resources.Messages;
using Fluxo.ViewModels.Popups.Settings;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public partial class QuickSetupWizardNamePageVM : ObservableObject
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMessenger _messenger;

    [ObservableProperty] private string _usernameText = "User";

    public QuickSetupWizardNamePageVM(IUnitOfWork unitOfWork, IMessenger? messenger = null)
    {
        _unitOfWork = unitOfWork;
        _messenger = messenger ?? WeakReferenceMessenger.Default;
    }

    public string ResolvedUsername => QuickSetupWizardShared.ResolveUsername(UsernameText);
    public bool HasUsernameInput => !string.IsNullOrWhiteSpace(UsernameText);

    public async Task LoadAsync()
    {
        var settings = await _unitOfWork.UserSettings.GetAllAsync();
        var settingsByName = settings.ToDictionary(setting => setting.Name, setting => setting.Value, StringComparer.Ordinal);

        UsernameText = QuickSetupWizardShared.ParseString(settingsByName, UserSettingNames.PreferredDisplayName, "User");

        PublishIdentitySnapshot();
    }

    public async Task<SettingsOperationResult> SaveAsync()
    {
        await ApplyAsync(_unitOfWork);
        await _unitOfWork.SaveChangesAsync();

        _messenger.Send(new DashboardDataInvalidatedMessage(DashboardDataInvalidationScope.All));
        PublishIdentitySnapshot();
        return SettingsOperationResult.Success();
    }

    public Task ApplyAsync(IUnitOfWork unitOfWork)
    {
        return QuickSetupWizardShared.UpsertUserSettingAsync(
            unitOfWork,
            UserSettingNames.PreferredDisplayName,
            ResolvedUsername);
    }

    partial void OnUsernameTextChanged(string value)
    {
        OnPropertyChanged(nameof(ResolvedUsername));
        OnPropertyChanged(nameof(HasUsernameInput));
        PublishIdentitySnapshot();
    }

    public string CurrentStepTitle => "What should fluxo call you?";

    public string CurrentStepDescription => "Pick the name you'd like fluxo to use throughout the app.";

    private void PublishIdentitySnapshot()
    {
        _messenger.Send(new QuickSetupWizardIdentityChangedMessage(
            new QuickSetupWizardIdentityChanged(ResolvedUsername)));
    }
}
