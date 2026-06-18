using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AccountDetailPopupBehaviorTests
{
    [Fact]
    public void EditButton_OpensAddAccountPopupInEditMode()
    {
        var source = ReadPopupCodeBehind();

        Assert.Contains("ShowAddAccount", source);
        Assert.Contains("CreateEditAccountViewModelAsync", source);
    }

    [Fact]
    public void HistorySection_ShowsNoHistoryPlaceholder()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("Text=\"No history found\"", xaml);
        Assert.Contains("HasRecentActivities", xaml);
    }

    [Fact]
    public void DisableButton_ConfirmsBeforeDisablingOnlyEnabledAccount()
    {
        var source = ReadPopupCodeBehind();

        Assert.Contains("ShouldConfirmDisablingOnlyEnabledSourceAsync", source);
        Assert.Contains("disable this account", source);
    }

    private static string ReadPopupXaml()
    {
        return File.ReadAllText(Path.Combine(
            GetRepositoryRootPath(),
            "Fluxo",
            "Views",
            "Popups",
            "AccountDetailPopup.xaml"));
    }

    private static string ReadPopupCodeBehind()
    {
        return File.ReadAllText(Path.Combine(
            GetRepositoryRootPath(),
            "Fluxo",
            "Views",
            "Popups",
            "AccountDetailPopup.xaml.cs"));
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
