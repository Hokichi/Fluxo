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
        Assert.Contains("x:Name=\"DebtIousTabButton\"", xaml);
        Assert.Contains("Style=\"{StaticResource LockedSettingsTabButtonStyle}\"", xaml);
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
        Assert.Contains("Text=\"{Binding TotalAmountText}\"", xaml);
        Assert.Contains("await viewModel.ResolveAsync(item);", codeBehind);
    }

    [Fact]
    public void SettingsDebtIousTab_UsesSettingsListItemHoverTemplate()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsDebtIousTab.xaml"));

        Assert.Contains("x:Name=\"ItemRoot\"", xaml);
        Assert.Contains("BorderBrush=\"Transparent\"", xaml);
        Assert.Contains("x:Name=\"RowActions\"", xaml);
        Assert.Contains("Visibility=\"Collapsed\"", xaml);
        Assert.Contains("SourceName=\"ItemRoot\" Property=\"IsMouseOver\" Value=\"True\"", xaml);
        Assert.Contains("TargetName=\"RowActions\" Property=\"Visibility\" Value=\"Visible\"", xaml);
        Assert.Contains("Height=\"1.5\"", xaml);
        Assert.Contains("Background=\"{StaticResource Brush.Border.Subtle}\"", xaml);
    }
}
