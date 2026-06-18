using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Shell.StartupWizard;

public sealed class StartupWizardScrollTests
{
    [Fact]
    public void MiddleStepNavigation_ResetsSharedScrollViewerToTop()
    {
        var middlePageXaml = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Shell",
            "Wizard",
            "Pages",
            "StartupWizardMiddlePage.xaml"));
        var middlePageCode = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Shell",
            "Wizard",
            "Pages",
            "StartupWizardMiddlePage.xaml.cs"));
        var wizardCode = File.ReadAllText(RepositoryPaths.File(
            "Fluxo",
            "Views",
            "Shell",
            "Wizard",
            "QuickSetupWizard.xaml.cs"));

        Assert.Contains("x:Name=\"MiddleStepScrollViewer\"", middlePageXaml);
        Assert.Contains("public void ScrollCurrentStepToTop()", middlePageCode);
        Assert.Contains("MiddleStepScrollViewer.ScrollToVerticalOffset(0);", middlePageCode);
        Assert.Contains("MiddleStepPage?.ScrollCurrentStepToTop();", wizardCode);
    }
}
