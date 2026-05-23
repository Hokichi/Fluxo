using Fluxo.Core.Entities;
using Fluxo.Core.Enums;
using Fluxo.Core.Interfaces.Services;
using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using NSubstitute;
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
    public void PaidAction_DoesNotRequireSourceSelection()
    {
        var vm = new NotificationChecklistActionVM(
        [
            new NotificationChecklistActionItemVM
            {
                EntityId = 1,
                Label = "Rent",
                SelectedAction = NotificationChecklistItemActionType.Paid,
                RequiresSourceSelection = true
            }
        ]);

        Assert.True(vm.CanProceed);
        Assert.True(vm.ProceedCommand.CanExecute(null));
        var decision = Assert.Single(vm.ActionDecisions);
        Assert.Equal(new NotificationChecklistActionDecision(1, NotificationChecklistItemActionType.Paid, null), decision);
    }

    [Fact]
    public void ShowRecurringFields_OnlyTrue_ForRecurringProcessRows()
    {
        var item = new NotificationChecklistActionItemVM
        {
            RecurringTransactionType = RecurringTransactionType.Expense,
            SelectedAction = NotificationChecklistItemActionType.Ignore
        };

        Assert.False(item.ShowRecurringFields);

        item.SelectedAction = NotificationChecklistItemActionType.Paid;
        Assert.False(item.ShowRecurringFields);

        item.SelectedAction = NotificationChecklistItemActionType.Process;
        Assert.True(item.ShowRecurringFields);

        item.RecurringTransactionType = null;
        Assert.False(item.ShowRecurringFields);
    }

    [Fact]
    public async Task RefreshAvailableTagsAsync_UpdatesItems_AndPreservesSelectedTag()
    {
        var appData = Substitute.For<IAppDataService>();
        appData.GetExpenseTagsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new ExpenseTag { Id = 1, Name = "Groceries", HexCode = "#22C55E" },
            new ExpenseTag { Id = 2, Name = "Transport", HexCode = "#38BDF8" }
        ]);

        var item = new NotificationChecklistActionItemVM { SelectedTagId = 1 };
        item.AvailableTags.Add(new ExpenseTagVM { Id = 1, Name = "Groceries", HexCode = "#22C55E" });
        var vm = new NotificationChecklistActionVM(appData, [item]);

        await vm.RefreshAvailableTagsAsync();

        Assert.Equal(1, item.SelectedTagId);
        Assert.Equal(new[] { 1, 2 }, item.AvailableTags.Select(tag => tag.Id).ToArray());
    }

    [Fact]
    public async Task ProcessAsync_ReturnsFalse_WhenNoProcessorAssigned()
    {
        var vm = new NotificationChecklistActionVM(
        [
            new NotificationChecklistActionItemVM
            {
                EntityId = 1,
                Label = "Rent",
                SelectedAction = NotificationChecklistItemActionType.Paid
            }
        ]);

        Assert.False(await vm.ProcessAsync());
    }

    [Fact]
    public async Task ProcessAsync_InvokesAssignedProcessor()
    {
        var invoked = false;
        var vm = new NotificationChecklistActionVM(
        [
            new NotificationChecklistActionItemVM
            {
                EntityId = 1,
                Label = "Rent",
                SelectedAction = NotificationChecklistItemActionType.Paid
            }
        ])
        {
            ProcessAsyncCallback = () =>
            {
                invoked = true;
                return Task.FromResult(true);
            }
        };

        var result = await vm.ProcessAsync();

        Assert.True(result);
        Assert.True(invoked);
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
