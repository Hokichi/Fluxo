using System;
using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed class InputBorderStateStyleTests
{
    [Fact]
    public void RoundedTextInputStyle_UsesMintOnFocus_AndDangerOnValidationError_WithInvalidWinning()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"RoundedTextInputStyle\"", "x:Key=\"RoundedMoneyTextInputStyle\"");

        var focusTrigger = "Property=\"IsKeyboardFocused\" Value=\"True\"";
        var invalidTrigger = "Property=\"Validation.HasError\" Value=\"True\"";
        var mintSetter = "Value=\"{DynamicResource Brush.Mint}\"";
        var dangerSetter = "Value=\"{DynamicResource Brush.Danger}\"";

        Assert.Contains(focusTrigger, styleSection);
        Assert.Contains(invalidTrigger, styleSection);
        Assert.Contains(mintSetter, styleSection);
        Assert.Contains(dangerSetter, styleSection);
        Assert.True(styleSection.IndexOf(focusTrigger, StringComparison.Ordinal) <
                    styleSection.IndexOf(invalidTrigger, StringComparison.Ordinal));
    }

    [Fact]
    public void RoundedMoneyTextInputStyle_UsesMintOnFocus_AndDangerOnValidationError_WithInvalidWinning()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"RoundedMoneyTextInputStyle\"", "</Style>");

        var focusTrigger = "Property=\"IsKeyboardFocused\" Value=\"True\"";
        var invalidTrigger = "Property=\"Validation.HasError\" Value=\"True\"";
        var mintSetter = "Value=\"{DynamicResource Brush.Mint}\"";
        var dangerSetter = "Value=\"{DynamicResource Brush.Danger}\"";

        Assert.Contains(focusTrigger, styleSection);
        Assert.Contains(invalidTrigger, styleSection);
        Assert.Contains(mintSetter, styleSection);
        Assert.Contains(dangerSetter, styleSection);
        Assert.True(styleSection.IndexOf(focusTrigger, StringComparison.Ordinal) <
                    styleSection.IndexOf(invalidTrigger, StringComparison.Ordinal));
    }

    [Fact]
    public void FluxoComboBoxStyle_UsesMintOnFocusWithin_AndDangerOnValidationError()
    {
        var globalStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo", "Resources", "Styles", "GlobalStyles.xaml"));
        var styleSection = ExtractSection(globalStylesXaml, "x:Key=\"FluxoComboBoxStyle\"", "x:Key=\"BudgetSliderThumbStyle\"");

        Assert.Contains("Property=\"IsKeyboardFocusWithin\" Value=\"True\"", styleSection);
        Assert.Contains("Property=\"Validation.HasError\" Value=\"True\"", styleSection);
        Assert.Contains("Value=\"{StaticResource Brush.Mint}\"", styleSection);
        Assert.Contains("Value=\"{StaticResource Brush.Danger}\"", styleSection);
    }

    [Fact]
    public void DateSelector_PropagatesValidationStateToSelectorButton_AndStyleSupportsMintAndDangerBorders()
    {
        var dateSelectorXaml = File.ReadAllText(ResolveRepoPath("Fluxo", "Views", "Components", "DateSelector.xaml"));
        var buttonStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo", "Resources", "Styles", "ButtonStyles.xaml"));
        var selectorStyleSection = ExtractSection(buttonStylesXaml, "x:Key=\"SelectorButtonStyle\"", "x:Key=\"CalendarNavButtonStyle\"");

        Assert.Contains("Tag=\"{Binding (Validation.HasError), ElementName=Root}\"", dateSelectorXaml);
        Assert.Contains("x:Name=\"SelectorBorder\"", selectorStyleSection);
        Assert.Contains("Property=\"IsKeyboardFocused\" Value=\"True\"", selectorStyleSection);
        Assert.Contains("Property=\"Tag\" Value=\"True\"", selectorStyleSection);
        Assert.Contains("Value=\"{StaticResource Brush.Mint}\"", selectorStyleSection);
        Assert.Contains("Value=\"{StaticResource Brush.Danger}\"", selectorStyleSection);
    }

    private static string ResolveRepoPath(params string[] relativeSegments)
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var solutionPath = Path.Combine(currentDirectory.FullName, "Fluxo.sln");
            var solutionXPath = Path.Combine(currentDirectory.FullName, "Fluxo.slnx");
            if (File.Exists(solutionPath) || File.Exists(solutionXPath))
                return Path.Combine(currentDirectory.FullName, Path.Combine(relativeSegments));

            currentDirectory = currentDirectory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
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
