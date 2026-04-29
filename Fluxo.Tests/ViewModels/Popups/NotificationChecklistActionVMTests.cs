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
    public void ProceedCommand_Disabled_WhenActionRequiresSourceButSourceMissing()
    {
        var vm = new NotificationChecklistActionVM(
        [
            new NotificationChecklistActionItemVM
            {
                EntityId = 1,
                Label = "Rent",
                SelectedAction = NotificationChecklistItemActionType.Process,
                RequiresSourceSelection = true
            }
        ]);

        Assert.False(vm.CanProceed);
        Assert.False(vm.ProceedCommand.CanExecute(null));
        Assert.Empty(vm.ActionDecisions);

        vm.Items[0].SelectedSourceId = 88;

        Assert.True(vm.CanProceed);
        Assert.True(vm.ProceedCommand.CanExecute(null));
        var decision = Assert.Single(vm.ActionDecisions);
        Assert.Equal(new NotificationChecklistActionDecision(1, NotificationChecklistItemActionType.Process, 88), decision);
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

    [Fact]
    public void ActionDecisions_ExcludesInvalidActionableRowsThatNeedSource()
    {
        var vm = new NotificationChecklistActionVM(
        [
            new NotificationChecklistActionItemVM
            {
                EntityId = 1,
                Label = "Rent",
                SelectedAction = NotificationChecklistItemActionType.Process,
                RequiresSourceSelection = true
            },
            new NotificationChecklistActionItemVM
            {
                EntityId = 2,
                Label = "Water",
                SelectedAction = NotificationChecklistItemActionType.Paid
            }
        ]);

        var decisions = vm.ActionDecisions.ToArray();

        var decision = Assert.Single(decisions);
        Assert.Equal(new NotificationChecklistActionDecision(2, NotificationChecklistItemActionType.Paid, null), decision);
    }

    [Fact]
    public void ItemsClear_UnsubscribesRemovedItemsFromPropertyChanged()
    {
        var item = new NotificationChecklistActionItemVM
        {
            EntityId = 1,
            Label = "Rent",
            SelectedAction = NotificationChecklistItemActionType.Paid
        };
        var vm = new NotificationChecklistActionVM([item]);

        var canProceedNotifications = 0;
        vm.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(NotificationChecklistActionVM.CanProceed), StringComparison.Ordinal))
                canProceedNotifications++;
        };

        vm.Items.Clear();
        canProceedNotifications = 0;

        item.SelectedAction = NotificationChecklistItemActionType.Ignore;

        Assert.Equal(0, canProceedNotifications);
    }
}
