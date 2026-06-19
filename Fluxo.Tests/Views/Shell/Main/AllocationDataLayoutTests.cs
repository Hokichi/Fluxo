using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class AllocationDataLayoutTests
{
    [Fact]
    public void AllocationData_UsesRoundedProgressBarsForSpentPercentages()
    {
        var xaml = ReadAllocationDataXaml();

        Assert.Contains("Value=\"{Binding NeedsPercentage, Mode=OneWay}\"", xaml);
        Assert.Contains("Value=\"{Binding WantsPercentage, Mode=OneWay}\"", xaml);
        Assert.Contains("Value=\"{Binding InvestPercentage, Mode=OneWay}\"", xaml);

        var progressBarCount = xaml.Split("<ProgressBar").Length - 1;
        Assert.Equal(3, progressBarCount);

        var roundedStyleCount = xaml.Split("Style=\"{StaticResource RoundedProgressBarStyle}\"").Length - 1;
        Assert.Equal(3, roundedStyleCount);
    }

    [Fact]
    public void AllocationData_ShowsRightAlignedSpentPercentageLabels()
    {
        var xaml = ReadAllocationDataXaml();

        Assert.Contains("HorizontalAlignment=\"Right\"", xaml);
        Assert.Contains("Text=\"{Binding NeedsPercentage, Mode=OneWay, StringFormat={}{0}% spent}\"", xaml);
        Assert.Contains("Text=\"{Binding WantsPercentage, Mode=OneWay, StringFormat={}{0}% spent}\"", xaml);
        Assert.Contains("Text=\"{Binding InvestPercentage, Mode=OneWay, StringFormat={}{0}% spent}\"", xaml);
    }

    private static string ReadAllocationDataXaml()
    {
        return File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Shell",
            "Main",
            "Sections",
            "AllocationData.xaml"));
    }
}
