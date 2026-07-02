using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class HotkeysOverviewPopupTests
{
    [Fact]
    public void PopupTitle_IsHotkeysOverview()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("PopupTitle=\"Hotkeys Overview\"", xaml);
    }

    [Fact]
    public void PopupGroupsHotkeysByExpectedSections()
    {
        var source = ReadPopupCodeBehind();

        AssertGroupOrder(
            source,
            "new HotkeyGroup(\"Global\"",
            "new HotkeyGroup(\"Dashboard\"",
            "new HotkeyGroup(\"Calendar\"",
            "new HotkeyGroup(\"Ledger\"",
            "new HotkeyGroup(\"Settings\"",
            "new HotkeyGroup(\"Other\"");
        Assert.DoesNotContain("new HotkeyGroup(\"Analytics\"", source);
    }

    [Fact]
    public void PopupLoadsHotkeysFromCodeBehindCollections()
    {
        var xaml = ReadPopupXaml();
        var source = ReadPopupCodeBehind();

        Assert.Contains("ItemsSource=\"{Binding HotkeyGroups}\"", xaml);
        Assert.Contains("public IReadOnlyList<HotkeyGroup> HotkeyGroups", source);
        Assert.Contains("public sealed record HotkeyGroup", source);
        Assert.Contains("public sealed record HotkeyItem", source);
        Assert.Contains("public sealed record HotkeyPart", source);
        Assert.DoesNotContain("Show hotkeys overview", xaml);
        Assert.Contains("new HotkeyItem(\"Show hotkeys overview\"", source);
    }

    [Fact]
    public void PageOpenShortcuts_AreGlobalNotPageSpecific()
    {
        var source = ReadPopupCodeBehind();
        var globalGroup = Slice(source, "new HotkeyGroup(\"Global\"", "new HotkeyGroup(\"Dashboard\"");
        var dashboardGroup = Slice(source, "new HotkeyGroup(\"Dashboard\"", "new HotkeyGroup(\"Calendar\"");
        var calendarGroup = Slice(source, "new HotkeyGroup(\"Calendar\"", "new HotkeyGroup(\"Ledger\"");
        var ledgerGroup = Slice(source, "new HotkeyGroup(\"Ledger\"", "new HotkeyGroup(\"Settings\"");

        Assert.Contains("Open dashboard", globalGroup);
        Assert.Contains("Open analytics", globalGroup);
        Assert.Contains("Open calendar", globalGroup);
        Assert.Contains("Open ledger", globalGroup);
        Assert.DoesNotContain("Open dashboard", dashboardGroup);
        Assert.DoesNotContain("Open calendar", calendarGroup);
        Assert.DoesNotContain("Open ledger", ledgerGroup);
    }

    [Fact]
    public void GroupsOrderHotkeysLogically()
    {
        var source = ReadPopupCodeBehind();
        var globalGroup = Slice(source, "new HotkeyGroup(\"Global\"", "new HotkeyGroup(\"Dashboard\"");
        var dashboardGroup = Slice(source, "new HotkeyGroup(\"Dashboard\"", "new HotkeyGroup(\"Calendar\"");
        var ledgerGroup = Slice(source, "new HotkeyGroup(\"Ledger\"", "new HotkeyGroup(\"Settings\"");

        AssertContainsInOrder(globalGroup, "Open dashboard", "Open analytics", "Open calendar", "Open ledger", "Search", "Open quick access", "Create transaction");
        AssertContainsInOrder(dashboardGroup, "Move to previous period", "Move to next period", "Move to current period");
        AssertContainsInOrder(ledgerGroup, "Sort amount ascending", "Sort amount descending", "Clear ledger filters", "Export ledger");
    }

    [Fact]
    public void PopupIncludesModifierShortcutsAndExcludesSimpleHotkeys()
    {
        var source = ReadPopupCodeBehind();

        Assert.Contains("Parts(\"Ctrl\", \"/\")", source);
        Assert.Contains("Parts(\"Ctrl\", \"Q\")", source);
        Assert.Contains("Parts(\"Ctrl\", \"1\")", source);
        Assert.Contains("Parts(\"Ctrl\", \"2\")", source);
        Assert.Contains("Parts(\"Ctrl\", \"3\")", source);
        Assert.Contains("Parts(\"Ctrl\", \"4\")", source);
        Assert.Contains("Parts(\"Ctrl\", \",\")", source);
        Assert.DoesNotContain("Parts(\"Enter\")", source);
        Assert.DoesNotContain("Parts(\"Esc\")", source);
    }

    private static void AssertGroupOrder(string source, params string[] markers)
    {
        var previousIndex = -1;

        foreach (var marker in markers)
        {
            var index = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"Expected '{marker}' to appear after the previous group.");
            previousIndex = index;
        }
    }

    private static void AssertContainsInOrder(string source, params string[] markers)
    {
        var previousIndex = -1;

        foreach (var marker in markers)
        {
            var index = source.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"Expected '{marker}' to appear after the previous marker.");
            previousIndex = index;
        }
    }

    private static string ReadPopupXaml()
    {
        var filePath = Path.Combine(GetRepositoryRootPath(), "Fluxo", "Views", "Popups", "HotkeysOverviewPopup.xaml");
        return File.ReadAllText(filePath);
    }

    private static string ReadPopupCodeBehind()
    {
        var filePath = Path.Combine(GetRepositoryRootPath(), "Fluxo", "Views", "Popups", "HotkeysOverviewPopup.xaml.cs");
        return File.ReadAllText(filePath);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");

        var end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find end marker '{endMarker}'.");

        return source[start..end];
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
