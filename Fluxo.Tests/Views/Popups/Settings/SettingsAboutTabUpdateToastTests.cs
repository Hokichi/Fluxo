using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsAboutTabUpdateToastTests
{
    [Fact]
    public void CheckForUpdates_PublishesAndDismissesFloatingProgress()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo", "Helper", "Settings", "SettingsUpdateCheckFlow.cs"));

        Assert.Contains("\"Checking for updates\"", source);
        Assert.Contains("FloatingNotificationPublisher.Publish", source);
        Assert.Contains("finally", source);
        Assert.Contains("FloatingNotificationPublisher.Dismiss", source);
        Assert.DoesNotContain("ShowToastWhileAsync", source);
    }

    [Fact]
    public void UpToDate_PublishesSpecificFloatingResult()
    {
        var source = File.ReadAllText(RepositoryPaths.File(
            "Fluxo", "Helper", "Settings", "SettingsUpdateCheckFlow.cs"));

        Assert.Contains("\"fluxo is up to date\"", source);
        Assert.Contains("NotificationSeverity.Success", source);
    }
}
