using System.Windows.Threading;
using Fluxo.Resources.CustomControls;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class CollapsingCardTests
{
    [Fact]
    public void IsExpanded_StartsOpen_AndTracksToggleState()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var card = new CollapsingCard();

                Assert.True(card.IsExpanded);
                card.IsExpanded = false;
                Assert.False(card.IsExpanded);
                card.IsExpanded = true;
                Assert.True(card.IsExpanded);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
    }
}
