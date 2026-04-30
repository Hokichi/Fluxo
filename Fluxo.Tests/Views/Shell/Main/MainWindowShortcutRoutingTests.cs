using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Shell.Main;

public sealed class MainWindowShortcutRoutingTests
{
    [Fact]
    public void CtrlNShortcut_RoutesToQuickAddPopup()
    {
        var filePath = Path.Combine(GetRepositoryRootPath(), "Fluxo", "Views", "Shell", "Main", "MainWindow.xaml.cs");
        var source = File.ReadAllText(filePath);

        Assert.Contains("if (MainWindowShortcutMatcher.IsOpenQuickAddShortcut(e.Key, Keyboard.Modifiers))", source);
        Assert.Contains("OpenQuickAddPopup();", source);
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
