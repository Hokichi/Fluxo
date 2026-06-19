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
    public void AccountCard_UsesTopAccentBarAndTopRightDotWithoutTypeText()
    {
        var xaml = File.ReadAllText(AccountXamlPath);

        var topBarIndex = xaml.IndexOf("Height=\"3\"", StringComparison.Ordinal);
        var dotIndex = xaml.IndexOf("x:Name=\"AccentBorder\"", StringComparison.Ordinal);

        Assert.True(topBarIndex >= 0);
        Assert.True(dotIndex > topBarIndex);
        Assert.Contains("VerticalAlignment=\"Top\"", xaml);
        Assert.Contains("Style=\"{StaticResource AccountAccentStyle}\"", xaml);
        Assert.DoesNotContain("Text=\"{Binding AccountType}\"", xaml);
    }
}
