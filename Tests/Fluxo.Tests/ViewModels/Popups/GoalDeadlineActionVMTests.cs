using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Popups;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups;

public sealed class GoalDeadlineActionVMTests
{
    [Fact]
    public void MarkAsReached_Disabled_WhenAmountEqualsRemaining()
    {
        var vm = new GoalDeadlineActionVM(
        [
            new SpendingSourceVM { Id = 1, Name = "Checking" }
        ])
        {
            RemainingAmount = 150m,
            EnteredAmount = 150m
        };

        Assert.False(vm.MarkAsReachedCommand.CanExecute(null));

        vm.EnteredAmount = 149m;

        Assert.True(vm.MarkAsReachedCommand.CanExecute(null));
    }

    [Fact]
    public void ActionCommands_SetSelectedAction()
    {
        var vm = new GoalDeadlineActionVM(
        [
            new SpendingSourceVM { Id = 1, Name = "Checking" }
        ])
        {
            RemainingAmount = 100m,
            EnteredAmount = 90m
        };

        vm.MarkAsReachedCommand.Execute(null);
        Assert.Equal(GoalDeadlineActionType.MarkAsReached, vm.SelectedAction);

        vm.AbandonGoalCommand.Execute(null);
        Assert.Equal(GoalDeadlineActionType.AbandonGoal, vm.SelectedAction);
    }
}
