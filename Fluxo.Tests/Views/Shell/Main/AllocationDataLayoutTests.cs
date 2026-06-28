using Fluxo.Tests.TestSupport;
using System.Text.RegularExpressions;
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

        var progressBarCount = Regex.Matches(xaml, @"<ProgressBar\s").Count;
        Assert.Equal(3, progressBarCount);

        var roundedStyleCount = xaml.Split("RoundedProgressBarStyle").Length - 1;
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

    [Fact]
    public void AllocationData_RendersOverflowStateForEverySegment()
    {
        var xaml = ReadAllocationDataXaml();

        Assert.Equal(3, xaml.Split("Path=\"{StaticResource ExclamationTriangle}\"").Length - 1);
        Assert.Equal(3, xaml.Split("Color=\"{StaticResource Brush.Danger}\"").Length - 1);
        Assert.Contains("IsNeedsOverflowing", xaml);
        Assert.Contains("IsWantsOverflowing", xaml);
        Assert.Contains("IsInvestOverflowing", xaml);
        Assert.Contains("NeedsRemainingDisplay", xaml);
        Assert.Contains("WantsRemainingDisplay", xaml);
        Assert.Contains("InvestRemainingDisplay", xaml);
        Assert.Contains("NeedsRemainingLabel", xaml);
        Assert.Contains("WantsRemainingLabel", xaml);
        Assert.Contains("InvestRemainingLabel", xaml);
        Assert.Equal(3, xaml.Split("Value=\"{StaticResource Brush.Danger}\"").Length - 1);
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
