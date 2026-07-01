using System;
using System.IO;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class BasePopupSplitButtonTests
{
    [Fact]
    public void BasePopup_DefinesShowSplitButtonDependencyPropertyAndClrAccessor()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());

        Assert.Contains("public static readonly DependencyProperty ShowSplitButtonProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(ShowSplitButton), typeof(bool), typeof(BasePopup),", source);
        Assert.Contains("public bool ShowSplitButton", source);
        Assert.Contains("get => (bool)GetValue(ShowSplitButtonProperty);", source);
        Assert.Contains("set => SetValue(ShowSplitButtonProperty, value);", source);
    }

    [Fact]
    public void BasePopup_DefinesSplitCheckedDependencyPropertyAndClrAccessor()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());

        Assert.Contains("public static readonly DependencyProperty IsSplitButtonCheckedProperty =", source);
        Assert.Contains("DependencyProperty.Register(nameof(IsSplitButtonChecked), typeof(bool), typeof(BasePopup)", source);
        Assert.Contains("public bool IsSplitButtonChecked", source);
        Assert.Contains("get => (bool)GetValue(IsSplitButtonCheckedProperty);", source);
        Assert.Contains("set => SetValue(IsSplitButtonCheckedProperty, value);", source);
    }

    [Fact]
    public void OnApplyTemplate_WiresSplitButtonToVirtualHandler()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "public override void OnApplyTemplate()");

        Assert.Contains("WireButton(\"PART_SplitButton\", _ => OnSplitButtonClick());", methodBody);
    }

    [Fact]
    public void BasePopup_DeclaresVirtualSplitButtonHandler()
    {
        var source = File.ReadAllText(ResolveBasePopupPath());
        var methodBody = ExtractMethodBodyBySignature(source, "protected virtual void OnSplitButtonClick()");

        Assert.NotNull(methodBody);
    }

    [Fact]
    public void PopupTemplate_DefinesSplitHeaderButtonWithExpectedBindings()
    {
        var source = File.ReadAllText(ResolvePopupStylesPath());

        Assert.Contains("x:Name=\"PART_SplitButton\"", source);
        Assert.Contains("<c:BalloonCheckBox", source);
        Assert.Contains("UncheckedIcon=\"{StaticResource ArrowsOutLineVertical}\"", source);
        Assert.Contains("UncheckedText=\"Split\"", source);
        Assert.Contains("CheckedText=\"Split\"", source);
        Assert.Contains("IsChecked=\"{Binding IsSplitButtonChecked, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}\"", source);
        Assert.Contains("Visibility=\"{TemplateBinding ShowSplitButton,", source);
        Assert.Contains("PopupBoolToVisibility", source);
    }

    [Fact]
    public void PopupTemplate_AllHeaderButtonsExpandWithFunctionalButtonText()
    {
        var source = File.ReadAllText(ResolvePopupStylesPath());

        AssertHeaderButtonExpandsWithText(source, "PART_CloneButton", "Clone");
        AssertHeaderButtonExpandsWithText(source, "PART_DeleteButton", "Delete");
        AssertHeaderButtonExpandsWithText(source, "PART_EditButton", "Edit");
        AssertHeaderButtonExpandsWithText(source, "PART_RevertButton", "Revert");
        AssertHeaderButtonExpandsWithText(source, "PART_ApplyButton", "Apply");
        AssertHeaderButtonExpandsWithText(source, "PART_CancelButton", "Cancel");
        AssertHeaderButtonExpandsWithText(source, "PART_SaveAndCreateNewButton", "Save and create new");
        AssertHeaderButtonExpandsWithText(source, "PART_SaveButton", "Save");
        AssertHeaderButtonExpandsWithText(source, "PART_CloseButton", "Close");
    }

    private static void AssertHeaderButtonExpandsWithText(string source, string partName, string buttonText)
    {
        var buttonNameIndex = source.IndexOf($"x:Name=\"{partName}\"", StringComparison.Ordinal);
        Assert.True(buttonNameIndex >= 0, $"Header button '{partName}' was not found.");

        var nextButtonIndex = source.IndexOf("<c:BalloonButton", buttonNameIndex + 1, StringComparison.Ordinal);
        var buttonMarkup = nextButtonIndex >= 0
            ? source[buttonNameIndex..nextButtonIndex]
            : source[buttonNameIndex..];

        Assert.Contains($"ButtonText=\"{buttonText}\"", buttonMarkup);
        Assert.Contains("ShouldExpand=\"True\"", buttonMarkup);
    }

    private static string ExtractMethodBodyBySignature(string source, string signatureMarker)
    {
        var signatureIndex = source.IndexOf(signatureMarker, StringComparison.Ordinal);
        Assert.True(signatureIndex >= 0, $"Method signature '{signatureMarker}' was not found in BasePopup.cs.");

        var openingBraceIndex = source.IndexOf('{', signatureIndex);
        Assert.True(openingBraceIndex >= 0, $"Opening brace for method signature '{signatureMarker}' was not found.");

        var depth = 0;
        for (var index = openingBraceIndex; index < source.Length; index++)
        {
            if (source[index] == '{')
            {
                depth++;
                continue;
            }

            if (source[index] != '}')
                continue;

            depth--;
            if (depth != 0)
                continue;

            return source.Substring(openingBraceIndex + 1, index - openingBraceIndex - 1);
        }

        throw new InvalidOperationException($"Closing brace for method signature '{signatureMarker}' was not found.");
    }

    private static string ResolveBasePopupPath()
    {
        return Path.Combine(ResolveRepositoryRootPath(), "Fluxo.Resources", "CustomControls", "BasePopup.cs");
    }

    private static string ResolvePopupStylesPath()
    {
        return Path.Combine(ResolveRepositoryRootPath(), "Fluxo.Resources", "Resources", "Styles", "PopupStyles.xaml");
    }

    private static string ResolveRepositoryRootPath()
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
