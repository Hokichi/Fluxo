using CommunityToolkit.Mvvm.Messaging;
using Fluxo.Resources.Components;
using Fluxo.Resources.Resources.Messages;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Components;

public sealed class ExpensesListLedgerNavigationTests
{
    [Fact]
    public void RequestLedgerNavigation_SendsMessage()
    {
        var messenger = new WeakReferenceMessenger();
        var recipient = new object();
        var received = false;
        messenger.Register<NavigateToLedgerRequestedMessage>(recipient, (_, _) => received = true);

        ExpensesList.RequestLedgerNavigation(messenger);

        Assert.True(received);
    }

    [Fact]
    public void ViewInLedgerButton_UsesMessengerHandler()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo.Resources", "Components", "ExpensesList.xaml"));

        Assert.Contains("ButtonText=\"View in Ledger\"", xaml);
        Assert.Contains("Click=\"OnViewInLedgerButtonClick\"", xaml);
    }
}
