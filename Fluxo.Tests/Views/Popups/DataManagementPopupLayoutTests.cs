using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class DataManagementPopupLayoutTests
{
    [Fact]
    public void DataManagementPopup_ContainsRequiredModesAndIcons()
    {
        var xaml = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "Fluxo",
            "Views",
            "Popups",
            "DataManagementPopup.xaml"));

        Assert.Contains("Backup", xaml);
        Assert.Contains("Append", xaml);
        Assert.Contains("Overwrite", xaml);
        Assert.Contains("DotsHorizontal", xaml);
        Assert.Contains("ControllerPlay", xaml);
    }

    [Fact]
    public void DataManagementPopup_ContainsFinalResultMessages()
    {
        var xaml = File.ReadAllText(Path.Combine(
            RepositoryPaths.Root,
            "Fluxo",
            "Views",
            "Popups",
            "DataManagementPopup.xaml"));

        Assert.Contains("ResultMessage", xaml);
        Assert.Contains("Result", xaml);
        Assert.Contains("Overwrite will replace existing data with backup data.", xaml);
    }
}
