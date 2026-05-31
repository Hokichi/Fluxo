using System.Windows.Controls;
using System.Windows.Documents;
using Fluxo.Resources.Infrastructure;
using Xunit;

namespace Fluxo.Tests.Views.Infrastructure;

public sealed class DependencyObjectTreeTests
{
    [Fact]
    public void FindAncestor_WalksFromRunInlineWithoutVisualParentException()
    {
        RunOnStaThread(() =>
        {
            var textBlock = new TextBlock();
            var run = new Run("source");
            textBlock.Inlines.Add(run);

            Assert.Same(textBlock, DependencyObjectTree.FindAncestor<TextBlock>(run));
        });
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
