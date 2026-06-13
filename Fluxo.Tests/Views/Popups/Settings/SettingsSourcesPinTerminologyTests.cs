using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups.Settings;

public sealed class SettingsSourcesPinTerminologyTests
{
    [Fact]
    public void SpendingSourceSettings_UsesPinUnpinActionNames()
    {
        var root = GetRepositoryRootPath();
        var xaml = File.ReadAllText(Path.Combine(root, "Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsSourcesTab.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "Fluxo", "Views", "Popups", "Settings", "Tabs", "SettingsSourcesTab.xaml.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "Fluxo", "ViewModels", "Popups", "Settings", "SettingsSourcesTabVM.cs"));

        Assert.Contains("SpendingSources:Unpin", xaml);
        Assert.Contains("SpendingSources:Pin", xaml);
        Assert.DoesNotContain("SpendingSources:Hide", xaml);
        Assert.DoesNotContain("SpendingSources:Unhide", xaml);
        Assert.DoesNotContain("SettingsBatchAction.Hide", codeBehind);
        Assert.DoesNotContain("SettingsBatchAction.Unhide", codeBehind);
        Assert.DoesNotContain("ShowSpendingSourceHideActionButton", viewModel);
        Assert.DoesNotContain("ShowSpendingSourceUnhideActionButton", viewModel);
    }

    [Fact]
    public void SpendingSourceDetail_UsesPinUnpinActionNames()
    {
        var root = GetRepositoryRootPath();
        var xaml = File.ReadAllText(Path.Combine(root, "Fluxo", "Views", "Popups", "SpendingSourceDetailPopup.xaml"));
        var codeBehind = File.ReadAllText(Path.Combine(root, "Fluxo", "Views", "Popups", "SpendingSourceDetailPopup.xaml.cs"));
        var viewModel = File.ReadAllText(Path.Combine(root, "Fluxo", "ViewModels", "Popups", "SpendingSourceDetailVM.cs"));

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
