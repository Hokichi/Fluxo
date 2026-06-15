using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsBudgetTabPeriodStartTests
{
    [Fact]
    public void SettingsBudgetTab_ConfigurationIncludesPeriodStartControls()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "Tabs",
            "SettingsBudgetTab.xaml"));

        Assert.Contains("Text=\"Period Start\"", xaml);
        Assert.Contains("WeekdayPeriodStartOptions", xaml);
        Assert.Contains("IsMonthlyPeriodStartVisible", xaml);
        Assert.Contains("QuarterlyPeriodStartOptions", xaml);
        Assert.Contains("YearlyPeriodStartOptions", xaml);
    }
}
