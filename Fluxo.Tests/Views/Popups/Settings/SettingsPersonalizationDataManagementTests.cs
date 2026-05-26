using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsPersonalizationDataManagementTests
{
    [Fact]
    public void PersonalizationTab_ContainsDataManagementButton()
    {
        var xaml = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "Tabs",
            "SettingsPersonalizationTab.xaml"));

        Assert.Contains("Data Management", xaml);
        Assert.Contains("OnDataManagementClick", xaml);
    }
}
