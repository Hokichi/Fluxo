using System.Text.RegularExpressions;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed partial class BackgroundResourceContractTests
{
    [Fact]
    public void BasePopup_UsesPopupBackground_WithoutOuterChromeBorder()
    {
        var popupStyles = Read("Fluxo.Resources", "Resources", "Styles", "PopupStyles.xaml");
        var basePopupStyle = ExtractSection(popupStyles, "TargetType=\"{x:Type c:BasePopup}\"", "</Style>");

        Assert.Contains("Background=\"{StaticResource Brush.Background.Popup}\"", basePopupStyle);
        Assert.DoesNotContain("BorderThickness=\"1\"", basePopupStyle);
        Assert.DoesNotContain("BorderBrush=\"{StaticResource Brush.Border.Subtle}\"", basePopupStyle);
        Assert.DoesNotContain("Background=\"{StaticResource Brush.Background.Main}\"", basePopupStyle);
    }

    [Theory]
    [InlineData("RoundedTextInputStyle")]
    [InlineData("RoundedPasswordInputStyle")]
    [InlineData("RoundedMoneyTextInputStyle")]
    [InlineData("RoundedSuffixTextInputStyle")]
    [InlineData("NumericUpDownStyle")]
    public void InputStyles_UseElevatedBackground(string styleKey)
    {
        var xaml = Read("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml");
        var section = ExtractStyleByKey(xaml, styleKey);

        Assert.Contains("Brush.Background.Elevated", section);
        Assert.DoesNotContain("Property=\"Background\" Value=\"{DynamicResource Brush.Background.Surface}\"", section);
        Assert.DoesNotContain("Property=\"Background\" Value=\"{StaticResource Brush.Background.Surface}\"", section);
    }

    [Fact]
    public void FluxoComboBox_UsesElevatedBackground_ForInputAndDropDownSurface()
    {
        var xaml = Read("Fluxo.Resources", "Resources", "Styles", "GlobalStyles.xaml");
        var section = ExtractStyleByKey(xaml, "FluxoComboBoxStyle");

        Assert.Contains("Property=\"Background\" Value=\"{StaticResource Brush.Background.Elevated}\"", section);
        Assert.Contains("Background=\"{StaticResource Brush.Background.Elevated}\"", section);
        Assert.DoesNotContain("Property=\"Background\" Value=\"{StaticResource Brush.Background.Surface}\"", section);
        Assert.DoesNotContain("Background=\"{StaticResource Brush.Background.Surface}\"", section);
    }

    [Fact]
    public void SharedCardStyle_UsesUnifiedCardValues()
    {
        var xaml = Read("Fluxo.Resources", "Resources", "Styles", "ContainerStyles.xaml");
        var section = ExtractStyleByKey(xaml, "CardBorderStyle");

        Assert.Contains("Property=\"Padding\" Value=\"16\"", section);
        Assert.Contains("Property=\"Background\" Value=\"{StaticResource Brush.Background.Surface}\"", section);
        Assert.Contains("Property=\"BorderBrush\" Value=\"{StaticResource Brush.Border.Subtle}\"", section);
        Assert.Contains("Property=\"BorderThickness\" Value=\"1\"", section);
        Assert.Contains("Property=\"CornerRadius\" Value=\"8\"", section);
        Assert.DoesNotContain("Brush.Background.Elevated", section);
        Assert.DoesNotContain("Brush.Background.Popup", section);
    }

    [Fact]
    public void SharedSeparatorStyles_UseUnifiedSeparatorValues()
    {
        var xaml = Read("Fluxo.Resources", "Resources", "Styles", "ContainerStyles.xaml");
        var vertical = ExtractStyleByKey(xaml, "VerticalSeparatorBorderStyle");
        var horizontal = ExtractStyleByKey(xaml, "HorizontalSeparatorBorderStyle");

        Assert.Contains("Property=\"Width\" Value=\"1.5\"", vertical);
        Assert.Contains("Property=\"Margin\" Value=\"12,0\"", vertical);
        Assert.Contains("Property=\"Background\" Value=\"{StaticResource Brush.Border.Subtle}\"", vertical);

        Assert.Contains("Property=\"Height\" Value=\"1.5\"", horizontal);
        Assert.Contains("Property=\"Margin\" Value=\"0,12\"", horizontal);
        Assert.Contains("Property=\"Background\" Value=\"{StaticResource Brush.Border.Subtle}\"", horizontal);
    }

    [Fact]
    public void ObsoleteAddFixedExpensePopup_IsDeleted()
    {
        Assert.False(File.Exists(Path.Combine(RepositoryPaths.Root, "Fluxo", "Views", "Popups", "AddFixedExpensePopup.xaml")));
        Assert.False(File.Exists(Path.Combine(RepositoryPaths.Root, "Fluxo", "Views", "Popups", "AddFixedExpensePopup.xaml.cs")));
    }

    [Fact]
    public void KnownPopupWrapperCards_AreRemoved()
    {
        var popupFiles = new[]
        {
            Path.Combine("Fluxo", "Views", "Popups", "AddAccountPopup.xaml"),
            Path.Combine("Fluxo", "Views", "Popups", "AddSavingGoalPopup.xaml"),
            Path.Combine("Fluxo", "Views", "Popups", "AddTagPopup.xaml"),
            Path.Combine("Fluxo", "Views", "Popups", "AddTagColorPickerPopup.xaml"),
            Path.Combine("Fluxo", "Views", "Popups", "DataManagementPopup.xaml")
        };

        foreach (var relativePath in popupFiles)
        {
            var xaml = File.ReadAllText(Path.Combine(RepositoryPaths.Root, relativePath));
            Assert.DoesNotContain("Style=\"{StaticResource PanelStyle}\"", xaml);
        }
    }

    [Fact]
    public void NoCardStyleBorder_IsNestedInsideAnotherCardStyleBorder()
    {
        var offenders = new List<string>();
        var xamlFiles = EnumerateXamlFiles();

        foreach (var file in xamlFiles)
        {
            var xaml = File.ReadAllText(file);
            var matches = CardBorderStartRegex().Matches(xaml).Cast<Match>().ToList();
            foreach (var outer in matches)
            {
                var outerCloseIndex = xaml.IndexOf("</Border>", outer.Index, StringComparison.Ordinal);
                if (outerCloseIndex < 0)
                    continue;

                foreach (var inner in matches.Where(match => match.Index > outer.Index && match.Index < outerCloseIndex))
                {
                    var line = xaml[..inner.Index].Count(c => c == '\n') + 1;
                    offenders.Add($"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{line}");
                }
            }
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void SeparatorLikeBorders_UseBorderSubtleAsBackground()
    {
        var offenders = new List<string>();

        foreach (var file in EnumerateXamlFiles())
        {
            var xaml = File.ReadAllText(file);
            foreach (Match match in SeparatorLikeBorderRegex().Matches(xaml))
            {
                var tag = match.Value;
                if (tag.Contains("Brush.Border.Subtle", StringComparison.Ordinal)
                    || tag.Contains("Style=\"{StaticResource VerticalSeparatorBorderStyle}\"", StringComparison.Ordinal)
                    || tag.Contains("Style=\"{StaticResource HorizontalSeparatorBorderStyle}\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var line = xaml[..match.Index].Count(c => c == '\n') + 1;
                offenders.Add($"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{line}");
            }
        }

        Assert.Empty(offenders);
    }

    private static IEnumerable<string> EnumerateXamlFiles()
    {
        return Directory.EnumerateFiles(RepositoryPaths.Root, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
    }

    private static string Read(params string[] relativeSegments)
    {
        return File.ReadAllText(Path.Combine(RepositoryPaths.Root, Path.Combine(relativeSegments)));
    }

    private static string ExtractStyleByKey(string content, string styleKey)
    {
        return ExtractSection(content, $"x:Key=\"{styleKey}\"", "</Style>");
    }

    private static string ExtractSection(string content, string startMarker, string endMarker)
    {
        var start = content.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find start marker '{startMarker}'.");

        var end = content.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find end marker '{endMarker}'.");

        return content[start..(end + endMarker.Length)];
    }

    [GeneratedRegex(@"<Border\b[^>]*Style=""\{StaticResource\s+(?:CardBorderStyle|PanelStyle|CardStyle|RowCardStyle|SectionCardStyle|DashboardPanelStyle|CalendarCardStyle|AnalyticsCardStyle|LedgerSummaryCardStyle)\}""[^>]*>", RegexOptions.Singleline)]
    private static partial Regex CardBorderStartRegex();

    [GeneratedRegex(@"<Border\b(?=[^>]*(?:Width=""1(?:\.5)?""|Height=""1(?:\.5)?""))(?=[^>]*Background=""\{(?:StaticResource|DynamicResource) Brush\.(?:Border\.Subtle|Background\.Subtle)\}"")[^>]*/?>", RegexOptions.Singleline)]
    private static partial Regex SeparatorLikeBorderRegex();
}
