using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class NotificationChecklistActionVMTests
{
    [Fact]
    public void DefaultAction_IsIgnore_AndProceedDisabled()
    {
        var vm = new NotificationChecklistActionVM(
        [
            new NotificationChecklistActionItemVM { EntityId = 1, Label = "Rent" },
            new NotificationChecklistActionItemVM { EntityId = 2, Label = "Water" }
        ]);

        Assert.All(vm.Items, item => Assert.Equal(NotificationChecklistItemActionType.Ignore, item.SelectedAction));
        Assert.Empty(vm.ActionDecisions);
        Assert.False(vm.CanProceed);
        Assert.False(vm.ProceedCommand.CanExecute(null));
    }

    [Fact]
    public void ProceedCommand_Enabled_WhenAnyRowIsPaidOrProcess()
    {
        var vm = new NotificationChecklistActionVM(
        [
            new NotificationChecklistActionItemVM { EntityId = 1, Label = "Rent" },
            new NotificationChecklistActionItemVM { EntityId = 2, Label = "Water" }
        ]);

        vm.Items[0].SelectedAction = NotificationChecklistItemActionType.Paid;
        Assert.True(vm.CanProceed);
        Assert.True(vm.ProceedCommand.CanExecute(null));

        vm.Items[0].SelectedAction = NotificationChecklistItemActionType.Process;
        Assert.True(vm.CanProceed);
        Assert.True(vm.ProceedCommand.CanExecute(null));
    }

    [Fact]
    public void ProceedCommand_SetsDidProceed_AndCapturesActionDecisions()
    {
        var vm = new NotificationChecklistActionVM(
        [
            new NotificationChecklistActionItemVM
            {
                EntityId = 1,
                Label = "Rent",
                SelectedAction = NotificationChecklistItemActionType.Paid
            },
            new NotificationChecklistActionItemVM
            {
                EntityId = 2,
                Label = "Water",
                SelectedAction = NotificationChecklistItemActionType.Process,
                SelectedSourceId = 18
            },
            new NotificationChecklistActionItemVM
            {
                EntityId = 3,
                Label = "Internet",
                SelectedAction = NotificationChecklistItemActionType.Ignore
            }
        ]);

        vm.ProceedCommand.Execute(null);

        Assert.True(vm.DidProceed);

        var decisions = vm.ActionDecisions.OrderBy(decision => decision.EntityId).ToArray();
        Assert.Equal(2, decisions.Length);

        Assert.Equal(new NotificationChecklistActionDecision(1, NotificationChecklistItemActionType.Paid, null), decisions[0]);
        Assert.Equal(new NotificationChecklistActionDecision(2, NotificationChecklistItemActionType.Process, 18), decisions[1]);
    }
}
