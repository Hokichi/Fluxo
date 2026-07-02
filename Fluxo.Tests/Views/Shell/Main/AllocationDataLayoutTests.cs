using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class AllocationDataLayoutTests
{
    [Fact]
    public void AllocationData_BindsReadOnlyPercentagesAndOverflowState()
    {
        var xaml = ReadAllocationDataXaml();

        Assert.Contains("Value=\"{Binding NeedsPercentage, Mode=OneWay}\"", xaml);
        Assert.Contains("Value=\"{Binding WantsPercentage, Mode=OneWay}\"", xaml);
        Assert.Contains("Value=\"{Binding InvestPercentage, Mode=OneWay}\"", xaml);

        Assert.Contains("IsNeedsOverflowing", xaml);
        Assert.Contains("IsWantsOverflowing", xaml);
        Assert.Contains("IsInvestOverflowing", xaml);
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
