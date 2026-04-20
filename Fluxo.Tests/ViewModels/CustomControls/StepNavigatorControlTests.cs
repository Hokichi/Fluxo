using System.Collections.ObjectModel;
using Fluxo.ViewModels.CustomControls;
using Fluxo.Views.CustomControls;
using Xunit;

namespace Fluxo.Tests.ViewModels.CustomControls;

public sealed class StepNavigatorControlTests
{
    [Fact]
    public void UpdateDotStates_CurrentStep1_FirstDotActiveOthersUpcoming()
    {
        var dots = MakeDots(3);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 1);

        Assert.True(dots[0].IsActive);
        Assert.False(dots[0].IsCompleted);
        Assert.False(dots[1].IsActive);
        Assert.False(dots[1].IsCompleted);
        Assert.False(dots[2].IsActive);
        Assert.False(dots[2].IsCompleted);
    }

    [Fact]
    public void UpdateDotStates_CurrentStep2_FirstCompletedSecondActive()
    {
        var dots = MakeDots(3);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 2);

        Assert.False(dots[0].IsActive);
        Assert.True(dots[0].IsCompleted);
        Assert.True(dots[1].IsActive);
        Assert.False(dots[1].IsCompleted);
        Assert.False(dots[2].IsActive);
        Assert.False(dots[2].IsCompleted);
    }

    [Fact]
    public void UpdateDotStates_CurrentStep3_TwoCompletedLastActive()
    {
        var dots = MakeDots(3);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 3);

        Assert.False(dots[0].IsActive);
        Assert.True(dots[0].IsCompleted);
        Assert.False(dots[1].IsActive);
        Assert.True(dots[1].IsCompleted);
        Assert.True(dots[2].IsActive);
        Assert.False(dots[2].IsCompleted);
    }

    [Fact]
    public void UpdateDotStates_NavigateBackward_PreviousCompletedBecomesUpcoming()
    {
        var dots = MakeDots(3);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 3);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 2);

        Assert.False(dots[0].IsActive);
        Assert.True(dots[0].IsCompleted);
        Assert.True(dots[1].IsActive);
        Assert.False(dots[1].IsCompleted);
        Assert.False(dots[2].IsActive);
        Assert.False(dots[2].IsCompleted);
    }

    [Fact]
    public void UpdateDotStates_ExactlyOneDotIsActive()
    {
        var dots = MakeDots(5);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 3);

        Assert.Equal(1, dots.Count(d => d.IsActive));
    }

    [Fact]
    public void UpdateDotStates_ActiveDotIsNeverCompleted()
    {
        var dots = MakeDots(5);
        StepNavigatorControl.UpdateDotStates(dots, currentStep: 3);

        Assert.False(dots[2].IsActive && dots[2].IsCompleted);
    }

    private static ObservableCollection<StepNavigatorDotVM> MakeDots(int count)
    {
        var dots = new ObservableCollection<StepNavigatorDotVM>();
        for (var i = 0; i < count; i++)
            dots.Add(new StepNavigatorDotVM { IsFirst = i == 0 });
        return dots;
    }
}
