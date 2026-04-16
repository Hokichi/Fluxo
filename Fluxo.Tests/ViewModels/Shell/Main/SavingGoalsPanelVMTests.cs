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
    }
}