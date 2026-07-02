using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsPersonalizationSaveTimingTests
{
    [Fact]
    public void PasswordEdits_UseTwoSecondRestartableTimer()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsPersonalizationTab.xaml.cs"));

        Assert.Contains("Interval = TimeSpan.FromSeconds(2)", source);
        Assert.Contains("_passwordAutosaveTimer.Stop();", source);
        Assert.Contains("_passwordAutosaveTimer.Start();", source);
        Assert.Contains("RequestPersonalizationAutosave();", source);
    }

    [Fact]
    public void NotificationPage_SuppressesAutosaveUntilExit()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo", "Views", "Popups", "Settings", "SettingsPopup.xaml.cs"));

        Assert.Contains("_viewModel.PersonalizationTab.IsNotificationPageSelected", source);
        Assert.Contains("\"Notifications updated\"", source);
        Assert.Contains("\"Password updated\"", source);
    }
}
