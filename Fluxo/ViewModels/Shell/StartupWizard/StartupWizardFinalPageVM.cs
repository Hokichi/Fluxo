using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Messages;

namespace Fluxo.ViewModels.Shell.StartupWizard;

public partial class StartupWizardFinalPageVM : ObservableRecipient, IRecipient<StartupWizardIdentityChangedMessage>
{
    [ObservableProperty] private string _resolvedUsername = "User";

    public StartupWizardFinalPageVM(IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        IsActive = true;
    }

    public void Receive(StartupWizardIdentityChangedMessage message)
    {
        ResolvedUsername = message.Value.ResolvedUsername;
    }
}

