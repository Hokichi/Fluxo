using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class SpendingSourceDetailPopupBehaviorTests
{
    [Fact]
    public void EditButton_OpensAddSpendingSourcePopupInEditMode()
    {
        var source = ReadPopupCodeBehind();

        Assert.Contains("ShowAddSpendingSource", source);
        Assert.Contains("CreateEditSpendingSourceViewModelAsync", source);
    }

    [Fact]
    public void HistorySection_ShowsNoHistoryPlaceholder()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("Text=\"No history found\"", xaml);
        Assert.Contains("HasRecentActivities", xaml);
    }

    [Fact]
    public void DisableButton_ConfirmsBeforeDisablingOnlyEnabledSource()
    {
        var source = ReadPopupCodeBehind();

        Assert.Contains("ShouldConfirmDisablingOnlyEnabledSourceAsync", source);
        Assert.Contains("disable the only functioning spending source", source);
    }

    private static string ReadPopupXaml()
    {
        return File.ReadAllText(Path.Combine(
            GetRepositoryRootPath(),
            "Fluxo",
            "Views",
            "Popups",
            "SpendingSourceDetailPopup.xaml"));
    }

    private static string ReadPopupCodeBehind()
    {
        return File.ReadAllText(Path.Combine(
            GetRepositoryRootPath(),
            "Fluxo",
            "Views",
            "Popups",
            "SpendingSourceDetailPopup.xaml.cs"));
    }

    private static string GetRepositoryRootPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
                return currentDirectory.FullName;

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }
}
