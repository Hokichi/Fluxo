using System;
using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsIoUsTabLayoutTests
{
    [Fact]
    public void SettingsPopup_WiresIoUsTab()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "SettingsPopup.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "SettingsPopup.xaml.cs"));

        Assert.Contains("Debt/IoUs", xaml);
        Assert.Contains("CreditCardMinusSolid", xaml);
        Assert.Contains("SettingsIoUsTab", xaml);
        Assert.Contains("x:Name=\"IoUsTabButton\"", xaml);
        Assert.Contains("Style=\"{StaticResource LockedSettingsTabButtonStyle}\"", xaml);
        Assert.Contains("IoUsTabButton", codeBehind);
        Assert.Contains("IoUsTabContent", codeBehind);
    }

    [Fact]
    public void SettingsIoUsTab_IncludesResolveButtonAndItems()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsDebtIousTab.xaml"));
        var codeBehind = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsDebtIousTab.xaml.cs"));

        Assert.Contains("ItemsSource=\"{Binding Items}\"", xaml);
        Assert.Contains("ButtonIcon=\"{StaticResource Check}\"", xaml);
        Assert.Contains("Click=\"OnResolveClick\"", xaml);
        Assert.Contains("Visibility=\"{Binding HasItems, Converter={StaticResource BoolToVisibilityInvertedConverter}}\"", xaml);
        Assert.Contains("Text=\"{Binding TotalAmountText, Mode=OneWay, Converter={StaticResource MoneyDisplayConverter}}\"", xaml);
        Assert.Contains("<Run Text=\"{Binding AmountSign, Mode=OneWay}\" />", xaml);
        Assert.Contains("<Run Text=\"{Binding Amount, Converter={StaticResource NumberWithCommasConverter}}\" />", xaml);
        Assert.Contains("<Run Text=\"{Binding Amount, Converter={StaticResource MoneyFullDisplayConverter}}\" />", xaml);
    }

}
