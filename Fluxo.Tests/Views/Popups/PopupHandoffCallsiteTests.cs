using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class PopupHandoffCallsiteTests
{
    [Theory]
    [InlineData("Fluxo", "Views", "Popups", "QuickAddPopup.xaml.cs")]
    [InlineData("Fluxo", "Views", "Popups", "AccountsListPopup.xaml.cs")]
    [InlineData("Fluxo", "Views", "Popups", "AccountDetailPopup.xaml.cs")]
    public void PopupHandoffCallsites_UseCloseForPopupHandoff(params string[] relativePathSegments)
    {
        var filePath = Path.Combine(GetRepositoryRootPath(), Path.Combine(relativePathSegments));
        var source = File.ReadAllText(filePath);

        Assert.Contains("CloseForPopupHandoff();", source);
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
