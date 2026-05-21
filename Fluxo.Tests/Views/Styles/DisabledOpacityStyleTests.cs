using System.Text.RegularExpressions;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed class DisabledOpacityStyleTests
{
    [Theory]
    [InlineData(@"Fluxo.Resources\Resources\Styles\ButtonStyles.xaml", "c:BalloonButton")]
    [InlineData(@"Fluxo.Resources\Resources\Styles\ButtonStyles.xaml", "SelectorButtonStyle")]
    [InlineData(@"Fluxo.Resources\Resources\Styles\ButtonStyles.xaml", "PopupTagToggleStyle")]
    [InlineData(@"Fluxo.Resources\Resources\Styles\GlobalStyles.xaml", "FluxoComboBoxStyle")]
    [InlineData(@"Fluxo.Resources\Resources\Styles\SettingsStyle.xaml", "ItemCheckBoxWithContentStyle")]
    public void DisabledControlStyles_DimToFortyPercent(string relativePath, string styleKey)
    {
        var xaml = File.ReadAllText(Path.Combine(RepositoryPaths.Root, relativePath));
        var styleMatch = Regex.Match(
            xaml,
            $@"<Style(?=[^>]*(?:x:Key=""{Regex.Escape(styleKey)}""|TargetType=""\{{x:Type {Regex.Escape(styleKey)}\}}""))(?<body>.*?)</Style>",
            RegexOptions.Singleline);

        Assert.True(styleMatch.Success, $"Could not find style {styleKey} in {relativePath}");
        var disabledTriggerMatches = Regex.Matches(
            styleMatch.Groups["body"].Value,
                @"<Trigger\s+Property=""IsEnabled""\s+Value=""False"">(?<body>.*?)</Trigger>",
            RegexOptions.Singleline);

        Assert.Contains(
            disabledTriggerMatches,
            match => Regex.IsMatch(match.Groups["body"].Value, @"Property=""Opacity""\s+Value=""0\.4"""));
    }

    [Theory]
    [InlineData(@"Fluxo\Views\Popups\AddNewTransaction.xaml", 197)]
    [InlineData(@"Fluxo\Views\Popups\NotificationChecklistActionPopup.xaml", 59)]
    public void InlineDisabledControls_DimToFortyPercent(string relativePath, int controlLine)
    {
        var path = Path.Combine(RepositoryPaths.Root, relativePath);
        var xaml = File.ReadAllText(path);
        var lineIndex = GetIndexOfLine(xaml, controlLine);
        var templateOrStyleStart = xaml.IndexOf("<ControlTemplate", lineIndex, StringComparison.Ordinal);
        if (templateOrStyleStart < 0)
            templateOrStyleStart = xaml.IndexOf("<Style", lineIndex, StringComparison.Ordinal);

        Assert.True(templateOrStyleStart >= 0, $"Could not find inline style/template after {relativePath}:{controlLine}");

        var closingTag = xaml.IndexOf("</ControlTemplate>", templateOrStyleStart, StringComparison.Ordinal);
        if (closingTag < 0)
            closingTag = xaml.IndexOf("</Style>", templateOrStyleStart, StringComparison.Ordinal);

        Assert.True(closingTag >= 0, $"Could not find end of inline style/template after {relativePath}:{controlLine}");

        var body = xaml[templateOrStyleStart..closingTag];
        var disabledTriggerMatch = Regex.Match(
            body,
            @"<Trigger\s+Property=""IsEnabled""\s+Value=""False"">(?<body>.*?)</Trigger>",
            RegexOptions.Singleline);

        Assert.True(disabledTriggerMatch.Success, $"Missing disabled trigger near {relativePath}:{controlLine}");
        Assert.Matches(@"Property=""Opacity""\s+Value=""0\.4""", disabledTriggerMatch.Groups["body"].Value);
    }

    [Fact]
    public void DisabledIsEnabledTemplateTriggers_DimToFortyPercent()
    {
        var failures = new List<string>();

        foreach (var path in EnumerateXamlFiles())
        {
            var xaml = File.ReadAllText(path);
            var triggerMatches = Regex.Matches(
                xaml,
                "<Trigger\\s+Property=\"IsEnabled\"\\s+Value=\"False\">(?<body>.*?)</Trigger>",
                RegexOptions.Singleline);

            foreach (Match triggerMatch in triggerMatches)
            {
                var body = triggerMatch.Groups["body"].Value;
                var opacityMatches = Regex.Matches(body, "Property=\"Opacity\"\\s+Value=\"(?<value>[^\"]+)\"");

                foreach (Match opacityMatch in opacityMatches)
                {
                    var opacity = opacityMatch.Groups["value"].Value;
                    if (opacity == "0.4")
                    {
                        continue;
                    }

                    var line = CountLinesBefore(xaml, opacityMatch.Index + triggerMatch.Index);
                    failures.Add($"{Path.GetRelativePath(RepositoryPaths.Root, path)}:{line} uses disabled opacity {opacity}");
                }
            }
        }

        Assert.Empty(failures);
    }

    private static IEnumerable<string> EnumerateXamlFiles() =>
        Directory.EnumerateFiles(RepositoryPaths.Root, "*.xaml", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

    private static int CountLinesBefore(string text, int index) =>
        text[..index].Count(character => character == '\n') + 1;

    private static int GetIndexOfLine(string text, int line)
    {
        var currentLine = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (currentLine == line)
                return i;

            if (text[i] == '\n')
                currentLine++;
        }

        return text.Length;
    }
}
