using System.Text.RegularExpressions;
using Fluxo.Tests.TestSupport;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed partial class ComboBoxStyleCoverageTests
{
    [Fact]
    public void ApplicationComboBoxes_UseNoGapDropDownStyles()
    {
        var fluxoRoot = Path.Combine(RepositoryPaths.Root, "Fluxo");
        var xamlFiles = Directory.EnumerateFiles(fluxoRoot, "*.xaml", SearchOption.AllDirectories);
        var unstyledComboBoxes = new List<string>();

        foreach (var file in xamlFiles)
        {
            var xaml = File.ReadAllText(file);
            foreach (Match match in ComboBoxStartTagRegex().Matches(xaml))
            {
                var startTag = match.Value;
                if (startTag.Contains("Style=\"{StaticResource FluxoComboBoxStyle}\"", StringComparison.Ordinal)
                    || startTag.Contains("Style=\"{StaticResource NotificationChecklistTagsComboBoxStyle}\"", StringComparison.Ordinal)
                    || startTag.Contains("Style=\"{StaticResource LedgerFilterComboStyle}\"", StringComparison.Ordinal)
                    || startTag.Contains("Style=\"{StaticResource LedgerGroupingComboStyle}\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var line = xaml[..match.Index].Count(c => c == '\n') + 1;
                unstyledComboBoxes.Add($"{Path.GetRelativePath(RepositoryPaths.Root, file)}:{line}");
            }
        }

        Assert.Empty(unstyledComboBoxes);
    }

    [GeneratedRegex(@"<ComboBox(?!\.)\b[^>]*>", RegexOptions.Singleline)]
    private static partial Regex ComboBoxStartTagRegex();
}
