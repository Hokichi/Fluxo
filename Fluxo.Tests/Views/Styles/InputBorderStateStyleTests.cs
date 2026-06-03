using System;
using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.Styles;

public sealed class InputBorderStateStyleTests
{
    [Fact]
    public void RoundedTextInputStyle_UsesMintOnFocus_AndDangerOnValidationError_WithInvalidWinning()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"RoundedTextInputStyle\"", "x:Key=\"RoundedMoneyTextInputStyle\"");

        Assert.Contains("Property=\"Validation.ErrorTemplate\" Value=\"{x:Null}\"", styleSection);

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
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml"));
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
    public void RoundedMoneyTextInputStyle_ShowsContentHostWhenFocused_ForEmptyAndZeroStates()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"RoundedMoneyTextInputStyle\"", "</Style>");

        Assert.Contains("<Condition Property=\"IsKeyboardFocused\" Value=\"True\" />", styleSection);
        Assert.Contains("<Condition Property=\"Text\" Value=\"\" />", styleSection);
        Assert.Contains("<Condition Property=\"IsZeroAmount\" Value=\"True\" />", styleSection);
        Assert.Contains("TargetName=\"PART_ContentHost\" Property=\"Visibility\" Value=\"Visible\"", styleSection);
        Assert.Contains("Property=\"Foreground\" Value=\"Transparent\"", styleSection);
    }

    [Fact]
    public void RoundedMoneyTextInputStyle_UsesTemplateBoundPlaceholderText()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"RoundedMoneyTextInputStyle\"", "x:Key=\"RoundedSuffixTextInputStyle\"");

        Assert.Contains("Text=\"{TemplateBinding PlaceholderText}\"", styleSection);
    }

    [Fact]
    public void RoundedSuffixTextInputStyle_UsesTemplateBoundPlaceholderText()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"RoundedSuffixTextInputStyle\"", "x:Key=\"NumericUpDownArrowButtonStyle\"");

        Assert.Contains("Text=\"{TemplateBinding PlaceholderText}\"", styleSection);
    }

    [Fact]
    public void RoundedSuffixTextInputStyle_Exists_TargetsSuffixTextBox_BindsSuffix_AndKeepsCoreStateTriggers()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"RoundedSuffixTextInputStyle\"", "x:Key=\"NumericUpDownArrowButtonStyle\"");

        Assert.Contains("TargetType=\"{x:Type customControls:SuffixTextBox}\"", styleSection);
        Assert.Contains("Text=\"{TemplateBinding Suffix}\"", styleSection);
        Assert.Contains("Property=\"IsKeyboardFocused\" Value=\"True\"", styleSection);
        Assert.Contains("TargetName=\"InputRoot\" Property=\"BorderBrush\" Value=\"{DynamicResource Brush.Mint}\"", styleSection);
        Assert.Contains("Property=\"Validation.HasError\" Value=\"True\"", styleSection);
        Assert.Contains("TargetName=\"InputRoot\" Property=\"BorderBrush\" Value=\"{DynamicResource Brush.Danger}\"", styleSection);
        Assert.Contains("<Trigger Property=\"IsZeroAmount\" Value=\"True\">", styleSection);
        Assert.Contains("TargetName=\"PlaceholderText\" Property=\"Visibility\" Value=\"Visible\"", styleSection);
        Assert.Contains("TargetName=\"PART_ContentHost\" Property=\"Visibility\" Value=\"Hidden\"", styleSection);
        Assert.Contains("<Condition Property=\"IsZeroAmount\" Value=\"True\" />", styleSection);
        Assert.Contains("Property=\"Foreground\" Value=\"Transparent\"", styleSection);
    }

    [Fact]
    public void RoundedTextInputStyle_UsesZeroVerticalPadding_ForCenteredText()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"RoundedTextInputStyle\"", "x:Key=\"RoundedMoneyTextInputStyle\"");

        Assert.Contains("<Setter Property=\"Padding\" Value=\"4,0\" />", styleSection);
        Assert.Contains("VerticalAlignment=\"Stretch\"", styleSection);
        Assert.DoesNotContain("VerticalAlignment=\"{TemplateBinding VerticalContentAlignment}\"", styleSection);
    }

    [Fact]
    public void RoundedMoneyTextInputStyle_UsesZeroVerticalPadding_ForCenteredTextAndPlaceholder()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"RoundedMoneyTextInputStyle\"", "x:Key=\"NumericUpDownArrowButtonStyle\"");

        Assert.Contains("<Setter Property=\"Padding\" Value=\"4,0\" />", styleSection);
        Assert.Contains("VerticalAlignment=\"Stretch\"", styleSection);
        Assert.Contains("Margin=\"12,0,0,0\"", styleSection);
        Assert.DoesNotContain("VerticalAlignment=\"{TemplateBinding VerticalContentAlignment}\"", styleSection);
    }

    [Fact]
    public void HeaderSearchTextBoxTemplate_StretchesContentHost_ForCenteredText()
    {
        var mainWindowXaml = File.ReadAllText(ResolveRepoPath("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml"));
        var textBoxSection = ExtractSection(mainWindowXaml, "x:Name=\"HeaderSearchBox\"", "</TextBox>");

        Assert.Contains("VerticalContentAlignment=\"Center\"", textBoxSection);
        Assert.Contains("VerticalAlignment=\"Stretch\"", textBoxSection);
        Assert.DoesNotContain("<customControls:FadingScrollViewer x:Name=\"PART_ContentHost\" Margin=\"0\" />", textBoxSection);
    }

    [Fact]
    public void HeaderSearchResultTemplate_UsesTransactionDirectionIconCircle()
    {
        var mainWindowXaml = File.ReadAllText(ResolveRepoPath("Fluxo", "Views", "Shell", "Main", "MainWindow.xaml"));
        var templateSection = ExtractSection(mainWindowXaml, "x:Key=\"HeaderSearchResultItemTemplate\"", "</DataTemplate>");

        Assert.Contains("Brush.Background.Surface", templateSection);
        Assert.Contains("BanknoteArrowUp", templateSection);
        Assert.Contains("BanknoteArrowDown", templateSection);
        Assert.Contains("Brush.Success", templateSection);
        Assert.Contains("Brush.Danger", templateSection);
        Assert.Contains("No transaction found", mainWindowXaml);
    }

    [Fact]
    public void SpendingSourceDetailInlineTextBoxTemplate_StretchesContentHost_ForCenteredText()
    {
        var popupXaml = File.ReadAllText(ResolveRepoPath("Fluxo", "Views", "Popups", "SpendingSourceDetailPopup.xaml"));
        var styleSection = ExtractSection(popupXaml, "x:Key=\"InlineHeaderTextBoxBaseStyle\"", "x:Key=\"InlineDetailValueTextBoxStyle\"");

        Assert.Contains("<Setter Property=\"VerticalContentAlignment\" Value=\"Center\" />", styleSection);
        Assert.Contains("VerticalAlignment=\"Stretch\"", styleSection);
        Assert.DoesNotContain("<customControls:FadingScrollViewer x:Name=\"PART_ContentHost\" Margin=\"0\" />", styleSection);
    }

    [Fact]
    public void NumericUpDownInnerTextBox_UsesZeroVerticalPadding_ForCenteredText()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"NumericUpDownStyle\"", "</Style>");

        Assert.Contains("Padding=\"4,0\"", styleSection);
        Assert.Contains("VerticalContentAlignment=\"Center\"", styleSection);
        Assert.DoesNotContain("Padding=\"4,6\"", styleSection);
    }

    [Fact]
    public void NumericUpDownStyle_UsesRoundedInputSurface_AndMintHoveredArrowIcons()
    {
        var textBoxStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "TextBoxStyles.xaml"));
        var styleSection = ExtractSection(textBoxStylesXaml, "x:Key=\"NumericUpDownStyle\"", "</Style>");

        Assert.Contains("CornerRadius=\"8\"", styleSection);
        Assert.Contains("Brush.Background.Surface", styleSection);
        Assert.Contains("Brush.Border.Subtle", styleSection);
        Assert.Contains("Path=\"{StaticResource AngleUp}\"", styleSection);
        Assert.Contains("Path=\"{StaticResource AngleDown}\"", styleSection);
        Assert.Contains("Property=\"IsMouseOver\" Value=\"True\"", styleSection);
        Assert.Contains("Value=\"{StaticResource Brush.Mint}\"", styleSection);
    }

    [Fact]
    public void FluxoComboBoxStyle_UsesMintOnFocusWithin_AndDangerOnValidationError()
    {
        var globalStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "GlobalStyles.xaml"));
        var styleSection = ExtractSection(globalStylesXaml, "x:Key=\"FluxoComboBoxStyle\"", "x:Key=\"BudgetSliderThumbStyle\"");

        Assert.Contains("Property=\"Validation.ErrorTemplate\" Value=\"{x:Null}\"", styleSection);
        Assert.Contains("Property=\"IsKeyboardFocusWithin\" Value=\"True\"", styleSection);
        Assert.Contains("Property=\"Validation.HasError\" Value=\"True\"", styleSection);
        Assert.Contains("Value=\"{StaticResource Brush.Mint}\"", styleSection);
        Assert.Contains("Value=\"{StaticResource Brush.Danger}\"", styleSection);
    }

    [Fact]
    public void DateSelector_PropagatesValidationStateToSelectorButton_AndStyleSupportsMintAndDangerBorders()
    {
        var dateSelectorXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Components", "DateSelector.xaml"));
        var buttonStylesXaml = File.ReadAllText(ResolveRepoPath("Fluxo.Resources", "Resources", "Styles", "ButtonStyles.xaml"));
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
