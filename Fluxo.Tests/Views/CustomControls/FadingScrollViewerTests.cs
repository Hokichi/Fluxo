using System.Threading;
using System.Windows.Controls;
using Fluxo.Resources.CustomControls;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class FadingScrollViewerTests
{
    [Theory]
    [InlineData(ScrollBarVisibility.Disabled, ScrollBarVisibility.Auto, -20, 10)]
    [InlineData(ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled, 10, -20)]
    [InlineData(ScrollBarVisibility.Hidden, ScrollBarVisibility.Hidden, 10, 10)]
    [InlineData(ScrollBarVisibility.Visible, ScrollBarVisibility.Visible, 10, 10)]
    public void CalculateFadedEdgeMargins_HidesOnlyEdgesForDisabledAxes(
        ScrollBarVisibility horizontalVisibility,
        ScrollBarVisibility verticalVisibility,
        double expectedHorizontalMargin,
        double expectedVerticalMargin)
    {
        RunOnStaThread(() =>
        {
            var scrollViewer = new FadingScrollViewer
            {
                HorizontalScrollBarVisibility = horizontalVisibility,
                VerticalScrollBarVisibility = verticalVisibility
            };

            var margin = scrollViewer.CalculateFadedEdgeMargins(100, 100, 100, 100);

            Assert.Equal(expectedHorizontalMargin, margin.Left);
            Assert.Equal(expectedVerticalMargin, margin.Top);
            Assert.Equal(expectedHorizontalMargin, margin.Right);
            Assert.Equal(expectedVerticalMargin, margin.Bottom);
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
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
