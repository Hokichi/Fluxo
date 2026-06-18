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

    [Fact]
    public void Dashboard_PlacesAllocationDataBesideAllowanceAboveAccounts()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Shell",
            "Main",
            "Pages",
            "Dashboard.xaml"));

        var allocationIndex = xaml.IndexOf("x:Name=\"AllocationDataHost\"", StringComparison.Ordinal);
        var allowanceIndex = xaml.IndexOf("x:Name=\"SpentAllowancePanelHost\"", StringComparison.Ordinal);
        var sourcesIndex = xaml.IndexOf("x:Name=\"DashboardAccountsScrollViewer\"", StringComparison.Ordinal);

        Assert.True(allocationIndex >= 0);
        Assert.True(allowanceIndex > allocationIndex);
        Assert.True(sourcesIndex > allowanceIndex);
        Assert.Contains("DataContext=\"{Binding AllocationData}\"", xaml);
    }
}
