using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Components;

public sealed class AccountComponentLayoutTests
{
    private static readonly string AccountXamlPath = RepositoryPaths.File(
        "Fluxo.Resources",
        "Components",
        "Account.xaml");

    [Fact]
    public void AccountCard_UsesAccountAccentBackgroundAndOutlineWithoutTypeText()
    {
        var xaml = File.ReadAllText(AccountXamlPath);

        Assert.Contains("x:Name=\"BGCard\"", xaml);
        Assert.Contains("Opacity=\"0.1\"", xaml);
        Assert.Contains("Background=\"Transparent\"", xaml);
        Assert.Contains("BorderThickness=\"2\"", xaml);
        Assert.Contains("Style=\"{StaticResource AccountAccentStyle}\"", xaml);
        Assert.DoesNotContain("Text=\"{Binding AccountType}\"", xaml);
    }

    [Fact]
    public void CreditAccountMenu_ProvidesRepaymentAction()
    {
        var xaml = File.ReadAllText(AccountXamlPath);
        var codeBehind = File.ReadAllText(RepositoryPaths.File(
            "Fluxo.Resources",
            "Components",
            "Account.xaml.cs"));

        Assert.Contains("Click=\"OnRepaymentActionClick\"", xaml);
        Assert.Contains("Path=\"{StaticResource Bill}\"", xaml);
        Assert.Contains("Text=\"Repayment\"", xaml);
        Assert.Contains("Visibility=\"{Binding CanRepay, Converter={StaticResource BoolToVisibilityConverter}}\"", xaml);
        Assert.Contains("OpenRepaymentPopup", codeBehind);
    }
}
