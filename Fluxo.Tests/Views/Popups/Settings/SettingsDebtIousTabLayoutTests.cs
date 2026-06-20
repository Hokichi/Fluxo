using System;
using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsDebtIousTabLayoutTests
{
    [Fact]
    public void SettingsPopup_WiresDebtIousTab()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "SettingsPopup.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "SettingsPopup.xaml.cs"));

        Assert.Contains("Debt/IoUs", xaml);
        Assert.Contains("CreditCardMinusSolid", xaml);
        Assert.Contains("SettingsDebtIousTab", xaml);
        Assert.Contains("DebtIousTabButton", codeBehind);
        Assert.Contains("DebtIousTabContent", codeBehind);
    }

    [Fact]
    public void SettingsDebtIousTab_IncludesResolveButtonAndItems()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsDebtIousTab.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsDebtIousTab.xaml.cs"));

        Assert.Contains("ItemsSource=\"{Binding Items}\"", xaml);
        Assert.Contains("ButtonIcon=\"{StaticResource Check}\"", xaml);
        Assert.Contains("Click=\"OnResolveClick\"", xaml);
        Assert.Contains("Visibility=\"{Binding HasItems, Converter={StaticResource BoolToVisibilityInvertedConverter}}\"", xaml);
        Assert.Contains("await viewModel.ResolveAsync(item);", codeBehind);
    }
}
