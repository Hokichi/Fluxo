using System.Reflection;
using System.Windows.Input;
using Fluxo.Resources.Components;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Components;

public sealed class TransactionsListDeleteCommandTests
{
    [Fact]
    public void DeleteButtonClick_ExecutesConfiguredCommandWithRow()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo.Resources", "Components", "TransactionsList.xaml"));
        var row = new object();
        var command = new RecordingCommand();
        var executeDelete = typeof(TransactionsList).GetMethod(
            "ExecuteDelete",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.Contains("Click=\"OnDeleteButtonClick\"", xaml);
        Assert.DoesNotContain("Command=\"{Binding DeleteCommand", xaml);
        Assert.NotNull(executeDelete);

        executeDelete.Invoke(null, [command, row]);

        Assert.Equal(1, command.ExecuteCount);
        Assert.Same(row, command.Parameter);
    }

    private sealed class RecordingCommand : ICommand
    {
        public int ExecuteCount { get; private set; }
        public object? Parameter { get; private set; }
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter)
        {
            ExecuteCount++;
            Parameter = parameter;
        }
    }
}
