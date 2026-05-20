using System.Text.RegularExpressions;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed class DisabledOpacityStyleTests
{
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
}
