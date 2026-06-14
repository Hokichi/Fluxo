using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public class DashboardLayoutTests
{
    [Fact]
    public void Dashboard_PlacesUpcomingEventsBesideSavingGoals()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Shell",
            "Main",
            "Pages",
            "Dashboard.xaml"));

        Assert.Contains("<sections:SavingGoalsPanel", xaml);
        Assert.Contains("<sections:UpcomingEventsPanel", xaml);
        Assert.Contains("DataContext=\"{Binding UpcomingEventsPanel}\"", xaml);
        Assert.DoesNotContain("x:Name=\"NotificationPanelPlaceholder\"", xaml);
    }
}
