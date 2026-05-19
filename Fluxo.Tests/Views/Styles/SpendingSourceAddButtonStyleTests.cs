using System.IO;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed class SpendingSourceAddButtonStyleTests
{
    [Fact]
    public void SpendingSourceAddButtonStyle_UsesButtonContentForLabel()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo.Resources",
            "Resources",
            "Styles",
            "ButtonStyles.xaml"));

        Assert.Contains("x:Key=\"SpendingSourceAddButtonStyle\"", xaml);
        Assert.Contains("Text=\"{TemplateBinding Content}\"", xaml);
    }
}
