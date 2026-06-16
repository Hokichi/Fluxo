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
    public void PopupStacksExpandableSectionsVertically()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("<StackPanel Orientation=\"Vertical\" />", xaml);
        Assert.Contains("<Expander", xaml);
        Assert.Contains("Header=\"{Binding Name}\"", xaml);
        Assert.Contains("Property=\"IsExpanded\" Value=\"True\"", xaml);
    }

    [Fact]
    public void PopupSplitsHotkeysInsideEachSectionIntoTwoColumns()
    {
        var xaml = ReadPopupXaml();
        var source = ReadPopupCodeBehind();

        Assert.Contains("ItemsSource=\"{Binding LeftHotkeys}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding RightHotkeys}\"", xaml);
        Assert.Contains("ItemTemplate=\"{StaticResource HotkeyItemTemplate}\"", xaml);
        Assert.Contains("<ColumnDefinition Width=\"16\" />", xaml);
        Assert.Contains("public IReadOnlyList<HotkeyItem> LeftHotkeys", source);
        Assert.Contains("public IReadOnlyList<HotkeyItem> RightHotkeys", source);
        Assert.Contains("var midpoint = (Hotkeys.Count + 1) / 2;", source);
    }

    [Fact]
    public void ExpanderHeaderUsesAngleIconsAndMintHoverAnimation()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("Data=\"{StaticResource AngleRight}\"", xaml);
        Assert.DoesNotContain("Data=\"{StaticResource AngleLeft}\"", xaml);
        Assert.Contains("Property=\"Data\" Value=\"{StaticResource AngleDown}\"", xaml);
        Assert.Contains("TargetName=\"ExpanderIcon\"", xaml);
        Assert.Contains("Width=\"10\"", xaml);
        Assert.Contains("Height=\"10\"", xaml);
        Assert.Contains("x:Name=\"ExpanderHeaderText\"", xaml);
        Assert.Contains("TargetName=\"ExpanderHeaderTextBrush\"", xaml);
        Assert.Contains("ColorAnimation", xaml);
        Assert.Contains("To=\"{StaticResource Color.Mint}\"", xaml);
        Assert.Contains("Trigger.EnterActions", xaml);
        Assert.Contains("Trigger.ExitActions", xaml);
        Assert.Contains("SourceName=\"ExpanderHeaderContent\"", xaml);
        Assert.DoesNotContain("TargetName=\"ExpanderHeaderButton\" Property=\"Background\"", xaml);
        Assert.DoesNotContain("TargetName=\"ExpanderHeaderButton\" Property=\"BorderBrush\"", xaml);
    }

    [Fact]
    public void HotkeyItemsHaveHoverEffect()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("x:Name=\"HotkeyItemBorder\"", xaml);
        Assert.Contains("Property=\"IsMouseOver\" Value=\"True\"", xaml);
        Assert.Contains("TargetName=\"HotkeyItemBorder\" Property=\"Background\" Value=\"{StaticResource Brush.Background.Hover}\"", xaml);
        Assert.Contains("TargetName=\"HotkeyItemBorder\" Property=\"BorderBrush\" Value=\"{StaticResource Brush.Mint}\"", xaml);
    }

    [Fact]
    public void HotkeyRowsSplitFunctionalityAndKeyParts()
    {
        var xaml = ReadPopupXaml();

        Assert.Contains("Text=\"{Binding Functionality}\"", xaml);
        Assert.Contains("ItemsSource=\"{Binding Parts}\"", xaml);
        Assert.Contains("Text=\"{Binding Text}\"", xaml);
        Assert.Contains("Grid.Column=\"2\"", xaml);
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
