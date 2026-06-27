using System.Windows.Input;
using Fluxo.Resources.CustomControls;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class BalloonToggleTests
{
    [Fact]
    public void Click_CyclesStatesThenReturnsToUntoggledFirstVisuals()
    {
        RunOnStaThread(() =>
        {
            var firstCommand = new CountingCommand();
            var secondCommand = new CountingCommand();
            var toggle = new TestBalloonToggle();
            toggle.States.Add(new BalloonToggleState { ButtonText = "First", OnChecked = firstCommand });
            toggle.States.Add(new BalloonToggleState { ButtonText = "Second", OnChecked = secondCommand });

            Assert.False(toggle.IsCycling);
            Assert.Equal("First", toggle.ButtonText);

            toggle.InvokeClick();
            Assert.True(toggle.IsCycling);
            Assert.Equal("First", toggle.ButtonText);
            Assert.Equal(1, firstCommand.ExecuteCount);

            toggle.InvokeClick();
            Assert.Equal("Second", toggle.ButtonText);
            Assert.Equal(1, secondCommand.ExecuteCount);

            toggle.InvokeClick();
            Assert.False(toggle.IsCycling);
            Assert.Equal("First", toggle.ButtonText);
            Assert.Equal(1, firstCommand.ExecuteCount);
            Assert.Equal(1, secondCommand.ExecuteCount);
        });
    }

    [Fact]
    public void Click_WithNoStates_RemainsUntoggled()
    {
        RunOnStaThread(() =>
        {
            var toggle = new TestBalloonToggle();
            toggle.InvokeClick();
            Assert.False(toggle.IsCycling);
        });
    }

    [Fact]
    public void Activate_SkipsCommandWhenCanExecuteIsFalse()
    {
        RunOnStaThread(() =>
        {
            var command = new CountingCommand(canExecute: false);
            var toggle = new TestBalloonToggle();
            toggle.States.Add(new BalloonToggleState { OnChecked = command });
            toggle.InvokeClick();
            Assert.True(toggle.IsCycling);
            Assert.Equal(0, command.ExecuteCount);
        });
    }

    [Fact]
    public void RemovingActiveState_ResetsToUntoggledFirstState()
    {
        RunOnStaThread(() =>
        {
            var first = new BalloonToggleState { ButtonText = "First" };
            var second = new BalloonToggleState { ButtonText = "Second" };
            var toggle = new TestBalloonToggle();
            toggle.States.Add(first);
            toggle.States.Add(second);
            toggle.InvokeClick();
            toggle.InvokeClick();

            toggle.States.Remove(second);

            Assert.False(toggle.IsCycling);
            Assert.Equal("First", toggle.ButtonText);
        });
    }

    private sealed class TestBalloonToggle : BalloonToggle
    {
        public void InvokeClick() => OnClick();
    }

    private sealed class CountingCommand(bool canExecute = true) : ICommand
    {
        public int ExecuteCount { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => canExecute;
        public void Execute(object? parameter) => ExecuteCount++;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception is not null)
            throw exception;
    }
}
