using Fluxo.ViewModels.Entities;
using Fluxo.ViewModels.Shell;
using Xunit;

namespace Fluxo.Tests.ViewModels.Shell.Main;

public class SavingGoalsPanelVMTests
{
    [Fact]
    public void LoadSnapshot_FiltersCompletedGoals()
    {
        var vm = new SavingGoalsPanelVM();
        var goals = new List<SavingGoalVM>
        {
            new()
            {
                Id = 1,
                Name = "Emergency Fund",
                TargetAmount = 1000m,
                CurrentAmount = 250m
            },
            new()
            {
                Id = 2,
                Name = "Laptop",
                TargetAmount = 1500m,
                CurrentAmount = 1500m
            }
        };

        vm.LoadSnapshot(goals);

        Assert.True(vm.HasSavingGoals);
        var remainingGoal = Assert.Single(vm.SavingGoals);
        Assert.Equal(1, remainingGoal.Id);
        Assert.Equal(0, vm.CurrentGoalIndex);
        Assert.Equal(1, vm.CurrentGoal?.Id);
        var activeDot = Assert.Single(vm.GoalDots, dot => dot.IsActive);
        Assert.Equal(vm.GoalDots[0], activeDot);
    }

    [Fact]
    public void NavigatePrevious_WrapsFromFirstToLastGoal()
    {
        var vm = new SavingGoalsPanelVM();
        vm.LoadSnapshot(CreateGoals(3));

        vm.NavigatePrevious();

        Assert.Equal(2, vm.CurrentGoalIndex);
        Assert.Equal(3, vm.CurrentGoal?.Id);
        Assert.Equal(1, vm.NavigationDirection);
        Assert.Equal(3, vm.GoalDots.Count);
        Assert.True(vm.GoalDots[2].IsActive);
    }

    [Fact]
    public void NavigateNext_WrapsFromLastToFirstGoal()
    {
        var vm = new SavingGoalsPanelVM();
        vm.LoadSnapshot(CreateGoals(2));

        vm.NavigatePrevious();
        vm.NavigateNext();

        Assert.Equal(0, vm.CurrentGoalIndex);
        Assert.Equal(1, vm.CurrentGoal?.Id);
        Assert.Equal(-1, vm.NavigationDirection);
        Assert.True(vm.GoalDots[0].IsActive);
        Assert.False(vm.GoalDots[1].IsActive);
    }

    private static IReadOnlyList<SavingGoalVM> CreateGoals(int count)
    {
        return Enumerable.Range(1, count)
            .Select(id => new SavingGoalVM
            {
                Id = id,
                Name = $"Goal {id}",
                TargetAmount = 1000m,
                CurrentAmount = 100m
            })
            .ToList();
    }
}
