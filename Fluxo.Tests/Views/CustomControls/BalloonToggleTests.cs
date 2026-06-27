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
    public void Click_SkipsStateWhoseCommandCannotExecute()
    {
        RunOnStaThread(() =>
        {
            var skipped = new CountingCommand(canExecute: false);
            var selected = new CountingCommand();
            var toggle = new TestBalloonToggle();
            toggle.States.Add(new BalloonToggleState { ButtonText = "Skipped", OnChecked = skipped });
            toggle.States.Add(new BalloonToggleState { ButtonText = "Selected", OnChecked = selected });

            toggle.InvokeClick();

            Assert.Equal("Selected", toggle.ButtonText);
            Assert.Equal(0, skipped.ExecuteCount);
            Assert.Equal(1, selected.ExecuteCount);
        });
    }

    [Fact]
    public void SelectState_IgnoresStateWhoseCommandCannotExecute()
    {
        RunOnStaThread(() =>
        {
            var state = new BalloonToggleState
            {
                ButtonText = "Unavailable",
                OnChecked = new CountingCommand(canExecute: false)
            };
            var toggle = new BalloonToggle();
            toggle.States.Add(state);

            toggle.SelectState(state);

            Assert.False(toggle.IsCycling);
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

    [Fact]
    public void OpenStatePopup_SuppressesCycle_AndSelectionActivatesState()
    {
        RunOnStaThread(() =>
        {
            var command = new CountingCommand();
            var state = new BalloonToggleState { ButtonText = "Chosen", OnChecked = command };
            var toggle = new TestBalloonToggle();
            toggle.States.Add(state);

            Assert.True(toggle.TryOpenStatePopup());
            toggle.InvokeClick();
            Assert.False(toggle.IsCycling);

            toggle.SelectState(state);
            Assert.True(toggle.IsCycling);
            Assert.Equal("Chosen", toggle.ButtonText);
            Assert.Equal(1, command.ExecuteCount);
        });
    }

    [Fact]
    public void OpenStatePopup_WithNoStates_DoesNothing()
    {
        RunOnStaThread(() => Assert.False(new BalloonToggle().TryOpenStatePopup()));
    }

    [Fact]
    public void SelectingActivePopupState_ExecutesCommandAgain()
    {
        RunOnStaThread(() =>
        {
            var command = new CountingCommand();
            var state = new BalloonToggleState { OnChecked = command };
            var toggle = new BalloonToggle();
            toggle.States.Add(state);

            toggle.SelectState(state);
            toggle.SelectState(state);

            Assert.True(toggle.IsCycling);
            Assert.Equal(2, command.ExecuteCount);
        });
    }

    [Fact]
    public void DefaultStyle_ContainsStatePopupAndStateButtons()
    {
        var xaml = File.ReadAllText(Fluxo.Tests.TestSupport.RepositoryPaths.File(
            "Fluxo.Resources", "Resources", "Styles", "ButtonStyles.xaml"));

        Assert.Contains("x:Name=\"PART_StatePopup\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding States, RelativeSource={RelativeSource TemplatedParent}}\"", xaml);
        Assert.Contains("CommandParameter=\"{Binding}\"", xaml);
    }

    [Fact]
    public void LongPressThreshold_IsThreeHundredMilliseconds()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(300), BalloonToggle.LongPressDuration);
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
