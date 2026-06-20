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

    [Fact]
    public void PersonalizationTab_UsesSegmentedAutoLockPresetSelector()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Popups",
            "Settings",
            "Tabs",
            "SettingsPersonalizationTab.xaml"));

        Assert.Contains("SelectedValue=\"{Binding SelectedAppAutoLockPreset, Mode=TwoWay}\"", xaml);
        Assert.Contains("Content=\"30 seconds\"", xaml);
        Assert.Contains("Content=\"1 minute\"", xaml);
        Assert.Contains("Content=\"3 minutes\"", xaml);
        Assert.Contains("Content=\"5 minutes\"", xaml);
        Assert.Contains("Content=\"10 minutes\"", xaml);
        Assert.Contains("Content=\"Custom\"", xaml);
        Assert.Contains("Visibility=\"{Binding IsCustomAutoLockInterval, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
    }
}
