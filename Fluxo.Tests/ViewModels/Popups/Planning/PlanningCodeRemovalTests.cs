using System.IO;
using Xunit;

namespace Fluxo.Tests.ViewModels.Popups.Planning;

public sealed class PlanningCodeRemovalTests
{
    [Theory]
    [InlineData("Fluxo", "ViewModels", "Popups", "Planning", "PlanningPopupVM.cs")]
    [InlineData("Fluxo", "ViewModels", "Popups", "Planning", "PlanningSnapshot.cs")]
    [InlineData("Fluxo", "Views", "Popups", "Planning", "PlanningPopup.xaml")]
    [InlineData("Fluxo", "Views", "Popups", "Planning", "PlanningPopup.xaml.cs")]
    public void OldPlanningFiles_AreRemoved(params string[] relativePathSegments)
    {
        Assert.False(File.Exists(Path.Combine(GetRepositoryRootPath(), Path.Combine(relativePathSegments))));
    }

    [Fact]
    public void DialogService_RoutesPlanningPopupToPlanningReport()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRootPath(),
            "Fluxo",
            "Services",
            "Dialogs",
            "DialogService.cs"));

        Assert.DoesNotContain("PlanningPopup", source);
        Assert.Contains("ShowPlanningReport", source);
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
