using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed class DynamicForegroundStyleTests
{
    [Fact]
    public void SharedTagTemplates_BindSelectedForegroundToSelectedBackgroundBrightness()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo.Resources", "Resources", "Styles", "MainWindowStyles.xaml"));
        var tagsTemplate = ExtractSection(xaml, "x:Key=\"Tags\"", "x:Key=\"PopupTagListViewItemStyle\"");
        var popupTagsTemplate = ExtractSection(xaml, "x:Key=\"PopupTags\"", "</DataTemplate>");

        Assert.Contains("x:Name=\"SelectedTagPill\"", tagsTemplate);
        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", tagsTemplate);
        Assert.Contains("ElementName=\"SelectedTagPill\"", tagsTemplate);
        Assert.DoesNotContain("<MultiBinding Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\">", tagsTemplate);
        Assert.Contains("Brush.Text.Primary", tagsTemplate);

        Assert.Contains("x:Name=\"SelectedPopupTagPill\"", popupTagsTemplate);
        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", popupTagsTemplate);
        Assert.Contains("ElementName=\"SelectedPopupTagPill\"", popupTagsTemplate);
        Assert.DoesNotContain("<MultiBinding Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\">", popupTagsTemplate);
        Assert.Contains("Brush.Text.Primary", popupTagsTemplate);
    }

    [Fact]
    public void PopupTagStyles_BindSelectedForegroundToSelectedBackgroundBrightness()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo.Resources", "Resources", "Styles", "ButtonStyles.xaml"));
        var toggleStyle = ExtractSection(xaml, "x:Key=\"PopupTagToggleStyle\"", "x:Key=\"PopupTagItemToggleStyle\"");
        var itemToggleStyle = ExtractSection(xaml, "x:Key=\"PopupTagItemToggleStyle\"", "x:Key=\"PopupTagItemRadioStyle\"");
        var radioStyle = ExtractSection(xaml, "x:Key=\"PopupTagItemRadioStyle\"", "x:Key=\"HeaderButtonStyle\"");

        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", toggleStyle);
        Assert.Contains("RelativeSource=\"{RelativeSource TemplatedParent}\"", toggleStyle);

        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", itemToggleStyle);
        Assert.Contains("x:Name=\"ColorDot\"", itemToggleStyle);

        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", radioStyle);
        Assert.Contains("x:Name=\"ColorDot\"", radioStyle);
    }

    [Fact]
    public void CalendarSelectedDayText_UsesFixedForegroundBrushes()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Calendar.xaml"));
        var dayTemplate = ExtractSection(xaml, "x:Key=\"CalendarDayTemplate\"", "x:Key=\"CalendarWeekTemplate\"");

        Assert.Contains("x:Name=\"CalendarDayButton\"", dayTemplate);
        Assert.Contains("Property=\"Foreground\" Value=\"{StaticResource Brush.Text.Primary}\"", dayTemplate);
        Assert.Contains("Brush.Text.Primary.Dark", dayTemplate);
        Assert.DoesNotContain("ForegroundForBackgroundBrushConverter", dayTemplate);
    }

    [Fact]
    public void LedgerDynamicBadges_BindForegroundToBadgeBackgroundBrightness()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "Pages", "Ledger.xaml"));

        Assert.Contains("x:Name=\"SelectionCountBadge\"", xaml);
        Assert.Contains("ElementName=\"SelectionCountBadge\"", xaml);
        Assert.Contains("x:Name=\"LedgerTransactionTagBadge\"", xaml);
        Assert.Contains("ElementName=\"LedgerTransactionTagBadge\"", xaml);
        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", xaml);
        Assert.DoesNotContain("<MultiBinding Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\">", xaml);
    }

    [Fact]
    public void HeaderSearchTagBadge_BindsForegroundToBadgeBackgroundBrightness()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml"));
        var template = ExtractSection(xaml, "x:Key=\"HeaderSearchResultItemTemplate\"", "</DataTemplate>");

        Assert.Contains("x:Name=\"HeaderSearchTagBadge\"", template);
        Assert.Contains("ElementName=\"HeaderSearchTagBadge\"", template);
        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", template);
        Assert.DoesNotContain("<MultiBinding Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\">", template);
    }

    [Fact]
    public void ExpenseDetailSplitTagToggle_BindsSelectedForegroundToSelectedBackgroundBrightness()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "ExpenseDetailPopup.xaml"));
        var style = ExtractSection(xaml, "x:Key=\"SplitRowTagToggleStyle\"", "</Style>");

        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", style);
        Assert.Contains("RelativeSource=\"{RelativeSource TemplatedParent}\"", style);
        Assert.Contains("<MultiBinding Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\">", style);
        Assert.Contains("Path=\"Foreground\" RelativeSource=\"{RelativeSource TemplatedParent}\"", style);
    }

    [Fact]
    public void AddTagColorPickerCheckIcon_BindsForegroundToColorChipBackgroundBrightness()
    {
        var xaml = File.ReadAllText(RepositoryPaths.File("Fluxo", "Views", "Popups", "AddTagPopup.xaml"));
        var style = ExtractSection(xaml, "TargetType=\"ListBoxItem\"", "</Style>");

        Assert.Contains("x:Name=\"ColorChip\"", style);
        Assert.Contains("Converter=\"{StaticResource ForegroundForBackgroundBrushConverter}\"", style);
        Assert.Contains("ElementName=\"ColorChip\"", style);
    }

    private static string ExtractSection(string content, string startMarker, string endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");

        var endSearchStart = start + startMarker.Length;
        var end = content.IndexOf(endMarker, endSearchStart, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find end marker '{endMarker}'.");

        return content[start..(end + endMarker.Length)];
    }
}
