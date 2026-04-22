using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class NotificationChecklistActionVMTests
{
    [Fact]
    public void ProceedCommand_Disabled_WhenNoChecklistSelection()
    {
        var vm = new NotificationChecklistActionVM(
        [
            new NotificationChecklistActionItemVM { EntityId = 1, Label = "Rent" },
            new NotificationChecklistActionItemVM { EntityId = 2, Label = "Water" }
        ]);

        Assert.False(vm.ProceedCommand.CanExecute(null));

        vm.Items[0].IsSelected = true;

        Assert.True(vm.ProceedCommand.CanExecute(null));
    }

    [Fact]
    public void ProceedCommand_SetsDidProceed_AndSelectedItems()
    {
        var vm = new NotificationChecklistActionVM(
        [
            new NotificationChecklistActionItemVM { EntityId = 1, Label = "Rent", IsSelected = true },
            new NotificationChecklistActionItemVM { EntityId = 2, Label = "Water" }
        ]);

        vm.ProceedCommand.Execute(null);

        Assert.True(vm.DidProceed);
        var selected = Assert.Single(vm.SelectedItems);
        Assert.Equal(1, selected.EntityId);
    }
}
