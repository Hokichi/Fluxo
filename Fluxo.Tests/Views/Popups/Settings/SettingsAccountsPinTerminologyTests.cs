using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsAccountsPinTerminologyTests
{
    [Fact]
    public void AccountSettings_UsesPinUnpinActionNames()
    {
        var root = GetRepositoryRootPath();
        var xaml = File.ReadAllText(Path.Combine(root, "Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsAccountsTab.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsAccountsTab.xaml.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "Fluxo", "ViewModels", "Popups", "Settings", "SettingsAccountsTabVM.cs"));

        Assert.Contains("Accounts:Unpin", xaml);
        Assert.Contains("Accounts:Pin", xaml);
        Assert.DoesNotContain("Accounts:Hide", xaml);
        Assert.DoesNotContain("Accounts:Unhide", xaml);
        Assert.DoesNotContain("SettingsBatchAction.Hide", codeBehind);
        Assert.DoesNotContain("SettingsBatchAction.Unhide", codeBehind);
        Assert.DoesNotContain("ShowAccountHideActionButton", viewModel);
        Assert.DoesNotContain("ShowAccountUnhideActionButton", viewModel);
    }

    [Fact]
    public void AccountDetail_UsesPinUnpinActionNames()
    {
        var root = GetRepositoryRootPath();
        var xaml = File.ReadAllText(Path.Combine(root, "Fluxo", "Views", "Popups", "AccountDetailPopup.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "Fluxo", "Views", "Popups", "AccountDetailPopup.xaml.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "Fluxo", "ViewModels", "Popups", "AccountDetailVM.cs"));

        Assert.Contains("OnPinOrUnpinButtonClick", xaml);
        Assert.Contains("CanPinOrUnpin", xaml);
        Assert.Contains("IsUnpinned", xaml);
        Assert.Contains("OnPinOrUnpinButtonClick", codeBehind);
        Assert.DoesNotContain("OnHideOrUnhideButtonClick", codeBehind);
        Assert.Contains("CanPinOrUnpin", viewModel);
        Assert.Contains("IsUnpinned", viewModel);
        Assert.DoesNotContain("CanHideOrUnhide", viewModel);
        Assert.DoesNotContain("IsHidden", viewModel);
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
