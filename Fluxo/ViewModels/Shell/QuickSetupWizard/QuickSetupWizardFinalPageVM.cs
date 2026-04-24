using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Messages;

namespace Fluxo.ViewModels.Shell.QuickSetupWizard;

public partial class QuickSetupWizardFinalPageVM : ObservableRecipient, IRecipient<QuickSetupWizardIdentityChangedMessage>
{
    [ObservableProperty] private string _resolvedUsername = "User";

    public QuickSetupWizardFinalPageVM(IMessenger? messenger = null)
        : base(messenger ?? WeakReferenceMessenger.Default)
    {
        IsActive = true;
    }

    public void Receive(QuickSetupWizardIdentityChangedMessage message)
    {
        ResolvedUsername = message.Value.ResolvedUsername;
    }
}

