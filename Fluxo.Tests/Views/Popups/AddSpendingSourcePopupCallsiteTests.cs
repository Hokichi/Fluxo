using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class AddSpendingSourcePopupCallsiteTests
{
    [Fact]
    public void AddSpendingSourcePopup_SaveFailure_UsesToastWarningBranch_AndNonModalWarningToast()
    {
        var source = File.ReadAllText(Path.Combine(
            GetRepositoryRootPath(),
            "Fluxo",
            "Views",
            "Popups",
            "AddSpendingSourcePopup.xaml.cs"));

        Assert.Contains(
            "result.FailurePresentation == AddSpendingSourceVM.AddSpendingSourceFailurePresentation.ToastWarning",
            source);
        Assert.Contains("ShowWarningToast(result.ErrorMessage);", source);
        Assert.Contains("popup.Show();", source);
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
