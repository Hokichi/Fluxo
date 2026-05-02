using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Popups;

public sealed class PlanningReportPopupKeyboardTests
{
    [Fact]
    public void OnPreviewKeyDown_UsesListAwareEnterHandling()
    {
        var source = File.ReadAllText(ResolvePlanningReportPopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "protected override void OnPreviewKeyDown(KeyEventArgs e)");

        Assert.Contains("if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)", methodBody);
        Assert.Contains("if (IsFocusedWithin(ExpenseScrollViewer))", methodBody);
        Assert.Contains("AddExpenseRow();", methodBody);
        Assert.Contains("else if (IsFocusedWithin(IncomeScrollViewer))", methodBody);
        Assert.Contains("AddIncomeRow();", methodBody);
        Assert.Contains("e.Handled = true;", methodBody);
        Assert.Contains("base.OnPreviewKeyDown(e);", methodBody);
    }

    [Fact]
    public void AddButtons_ReuseSharedRowAddMethods()
    {
        var source = File.ReadAllText(ResolvePlanningReportPopupPath());
        var addIncomeBody = ExtractMethodBodyBySignature(source, "private void OnAddIncomeClick(object sender, RoutedEventArgs e)");
        var addExpenseBody = ExtractMethodBodyBySignature(source, "private void OnAddExpenseClick(object sender, RoutedEventArgs e)");

        Assert.Contains("AddIncomeRow();", addIncomeBody);
        Assert.Contains("AddExpenseRow();", addExpenseBody);
    }

    private static string ResolvePlanningReportPopupPath()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);

        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
            {
                return Path.Combine(
                    currentDirectory.FullName,
                    "Fluxo",
                    "Views",
                    "Popups",
                    "Planning",
                    "PlanningReportPopup.xaml.cs");
            }

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository root containing 'Fluxo.sln' or 'Fluxo.slnx' from '{AppContext.BaseDirectory}'.");
    }

    private static string ExtractMethodBodyBySignature(string source, string signatureMarker)
    {
        var signatureIndex = source.IndexOf(signatureMarker, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature '{signatureMarker}' was not found.");

        var bodyStart = source.IndexOf('{', signatureIndex);
        Assert.True(bodyStart >= 0, $"Opening brace for '{signatureMarker}' was not found.");

        var braceDepth = 0;
        for (var i = bodyStart; i < source.Length; i++)
        {
            if (source[i] == '{')
                braceDepth++;
            else if (source[i] == '}')
                braceDepth--;

            if (braceDepth != 0)
                continue;

            return source.Substring(bodyStart + 1, i - bodyStart - 1);
        }

        throw new InvalidDataException($"Closing brace for '{signatureMarker}' was not found.");
    }
}
